// Feature: domain-event-handlers, Property 1: Dispatcher publishes exactly all domain events

using CsCheck;
using Flowmetry.Domain;
using Flowmetry.Infrastructure;
using Flowmetry.Infrastructure.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flowmetry.API.Tests.PropertyTests;

/// <summary>
/// Property-based tests for DomainEventDispatchInterceptor.
/// Property 1: Dispatcher publishes exactly all domain events
/// </summary>
public class DomainEventDispatcherPropertyTests
{
    // ── Property 1: Dispatcher publishes exactly all domain events ────────────
    // Validates: Requirements 1.1, 1.5

    [Fact]
    public async Task Property1_DispatcherPublishesExactlyAllDomainEvents()
    {
        // **Validates: Requirements 1.1, 1.5**
        //
        // For any set of tracked Invoice aggregates each carrying any number of
        // domain events, after SaveChanges completes the dispatcher should have
        // published exactly as many notifications as the total number of domain
        // events across all aggregates (including zero when no events are present).

        // Generator: list of 0..5 invoices, each with a random depth of domain events
        var invoiceListGen =
            from count in Gen.Int[0, 5]
            from depths in Gen.Int[1, 4].Array[count]
            select depths;

        await invoiceListGen.SampleAsync(async depths =>
        {
            var spy = new SpyPublisher();
            var interceptor = new DomainEventDispatchInterceptor(
                new FakeScopeFactory(spy),
                NullLogger<DomainEventDispatchInterceptor>.Instance);

            await using var context = CreateInMemoryContext(interceptor);

            // Build invoices and track them in the context
            var invoices = depths.Select(d => BuildInvoiceWithDepth(d)).ToList();
            var expectedCount = invoices.Sum(i => i.DomainEvents.Count);

            foreach (var invoice in invoices)
                context.Invoices.Attach(invoice);

            // Trigger SaveChangesAsync — the interceptor fires via the post-save hook
            await context.SaveChangesAsync();

            Assert.Equal(expectedCount, spy.PublishCount);
        });
    }

    // ── Property 2: Dispatcher clears events after publishing ─────────────────
    // Validates: Requirements 1.2

    [Fact]
    public async Task Property2_DispatcherClearsEventsAfterPublishing()
    {
        // Feature: domain-event-handlers, Property 2: Dispatcher clears events after publishing
        //
        // **Validates: Requirements 1.2**
        //
        // For any set of tracked Invoice aggregates, after SaveChanges completes
        // every tracked invoice should have an empty DomainEvents collection.

        var invoiceListGen =
            from count in Gen.Int[0, 5]
            from depths in Gen.Int[1, 4].Array[count]
            select depths;

        await invoiceListGen.SampleAsync(async depths =>
        {
            var spy = new SpyPublisher();
            var interceptor = new DomainEventDispatchInterceptor(
                new FakeScopeFactory(spy),
                NullLogger<DomainEventDispatchInterceptor>.Instance);

            await using var context = CreateInMemoryContext(interceptor);

            var invoices = depths.Select(d => BuildInvoiceWithDepth(d)).ToList();

            foreach (var invoice in invoices)
                context.Invoices.Attach(invoice);

            await context.SaveChangesAsync();

            foreach (var invoice in invoices)
                Assert.Empty(invoice.DomainEvents);
        });
    }

    // ── Property 3: Dispatcher preserves event order ──────────────────────────
    // Validates: Requirements 1.4

    [Fact]
    public async Task Property3_DispatcherPreservesEventOrder()
    {
        // Feature: domain-event-handlers, Property 3: Dispatcher preserves event order
        //
        // **Validates: Requirements 1.4**
        //
        // For any Invoice aggregate with multiple domain events, the sequence in
        // which the dispatcher publishes those events should match the order they
        // appear in DomainEvents before SaveChanges is called.

        // Generator: a single invoice with depth 2-4 (guarantees multiple events)
        var depthGen = Gen.Int[2, 4];

        await depthGen.SampleAsync(async depth =>
        {
            var spy = new SpyPublisher();
            var interceptor = new DomainEventDispatchInterceptor(
                new FakeScopeFactory(spy),
                NullLogger<DomainEventDispatchInterceptor>.Instance);

            await using var context = CreateInMemoryContext(interceptor);

            var invoice = BuildInvoiceWithDepth(depth);

            // Capture the original event order BEFORE SaveChangesAsync
            var originalEvents = invoice.DomainEvents.ToList();

            context.Invoices.Attach(invoice);
            await context.SaveChangesAsync();

            var publishedNotifications = spy.Published;

            // The number of published notifications must match the original event count
            Assert.Equal(originalEvents.Count, publishedNotifications.Count);

            // Each published notification must wrap the corresponding original event
            // in the same order — extract the inner event via reflection
            for (var i = 0; i < originalEvents.Count; i++)
            {
                var notification = publishedNotifications[i];
                var eventProp = notification.GetType().GetProperty("Event");
                var innerEvent = eventProp?.GetValue(notification);

                Assert.Same(originalEvents[i], innerEvent);
            }
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds an Invoice with a specific number of domain events by calling
    /// domain methods up to the given depth:
    ///   depth 1 → Create  (InvoiceCreated)
    ///   depth 2 → + MarkAsSent  (InvoiceSent)
    ///   depth 3 → + RecordPayment  (PaymentReceived)
    ///   depth 4 → + MarkOverdue  (InvoiceOverdue)
    /// </summary>
    private static Invoice BuildInvoiceWithDepth(int depth)
    {
        var invoice = Invoice.Create(Guid.NewGuid(), 100m, DateOnly.FromDateTime(DateTime.Today.AddDays(30)));

        if (depth >= 2)
            invoice.MarkAsSent();

        if (depth >= 3)
            invoice.RecordPayment(50m);

        if (depth >= 4)
            invoice.MarkOverdue();

        return invoice;
    }

    private static FlowmetryDbContext CreateInMemoryContext(DomainEventDispatchInterceptor interceptor)
    {
        var options = new DbContextOptionsBuilder<FlowmetryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new FlowmetryDbContext(options);
    }
}

// ── SpyPublisher ──────────────────────────────────────────────────────────────
// Counts every Publish call and records published notifications in order.

internal sealed class SpyPublisher : IPublisher
{
    private int _count;
    private readonly List<INotification> _published = new();
    private readonly object _lock = new();

    public int PublishCount => _count;

    /// <summary>Published notifications in the order they were received.</summary>
    public IReadOnlyList<INotification> Published
    {
        get { lock (_lock) { return _published.ToList(); } }
    }

    public Task Publish(object notification, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _count);
        return Task.CompletedTask;
    }

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        Interlocked.Increment(ref _count);
        lock (_lock) { _published.Add(notification); }
        return Task.CompletedTask;
    }
}

// ── FakeScopeFactory ──────────────────────────────────────────────────────────
// Wraps a SpyPublisher in a minimal IServiceScopeFactory so the interceptor
// (which now resolves IPublisher from a scope) can be tested without a real DI container.

internal sealed class FakeScopeFactory(SpyPublisher publisher) : IServiceScopeFactory
{
    public IServiceScope CreateScope() => new FakeScope(publisher);

    private sealed class FakeScope(SpyPublisher publisher) : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = new FakeServiceProvider(publisher);
        public void Dispose() { }
    }

    private sealed class FakeServiceProvider(SpyPublisher publisher) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(IPublisher) ? publisher : null;
    }
}

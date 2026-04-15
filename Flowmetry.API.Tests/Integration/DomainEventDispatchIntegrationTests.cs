using System.Collections.Concurrent;
using System.Net.Http.Json;
using Flowmetry.Application.Common;
using Flowmetry.Application.Invoices.Services;
using Flowmetry.Infrastructure;
using Flowmetry.Infrastructure.Events;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Flowmetry.API.Tests.Integration;

// ── Spy ──────────────────────────────────────────────────────────────────────

/// <summary>
/// Singleton spy that captures every ScheduleAsync call across DI scopes.
/// Registered as singleton so it survives the HTTP request scope boundary.
/// </summary>
public sealed class SpyReminderScheduler : IReminderScheduler
{
    public ConcurrentBag<(Guid InvoiceId, ReminderType Type, DateTimeOffset ScheduledAt)> ScheduleCalls { get; } = new();
    public ConcurrentBag<(Guid InvoiceId, ReminderType Type, DateTimeOffset ScheduledAt)> CancelOrReplaceCalls { get; } = new();

    public Task<ServiceResult> ScheduleAsync(Guid invoiceId, ReminderType type, DateTimeOffset scheduledAt, CancellationToken ct = default)
    {
        ScheduleCalls.Add((invoiceId, type, scheduledAt));
        return Task.FromResult<ServiceResult>(new ServiceResult.Success());
    }

    public Task<ServiceResult> CancelOrReplaceAsync(Guid invoiceId, ReminderType type, DateTimeOffset scheduledAt, CancellationToken ct = default)
    {
        CancelOrReplaceCalls.Add((invoiceId, type, scheduledAt));
        return Task.FromResult<ServiceResult>(new ServiceResult.Success());
    }
}

// ── Factory ───────────────────────────────────────────────────────────────────

public class EventDispatchWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"EventDispatchTestDb-{Guid.NewGuid()}";

    /// <summary>Singleton spy shared across all DI scopes within this factory.</summary>
    public SpyReminderScheduler SpyScheduler { get; } = new();

    public static readonly Guid SeededCustomerId = Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("DATABASE_URL", "Host=localhost;Database=test;Username=test;Password=test");

        builder.ConfigureServices(services =>
        {
            // Replace Npgsql with InMemory, but keep the interceptor wired in
            RemoveDbContextRegistrations(services);
            services.AddDbContext<FlowmetryDbContext>((sp, options) =>
            {
                options.UseInMemoryDatabase(_dbName)
                       .EnableSensitiveDataLogging();
                // Re-wire the interceptor the same way production code does
                options.AddInterceptors(
                    new DomainEventDispatchInterceptor(
                        sp.GetRequiredService<IServiceScopeFactory>(),
                        sp.GetRequiredService<ILoggerFactory>().CreateLogger<DomainEventDispatchInterceptor>()));
            });

            // Replace the scoped IReminderScheduler with our singleton spy
            services.RemoveAll<IReminderScheduler>();
            services.AddSingleton<IReminderScheduler>(SpyScheduler);
        });
    }

    private static void RemoveDbContextRegistrations(IServiceCollection services)
    {
        var contextType = typeof(FlowmetryDbContext);
        var optionsConfigType = typeof(IDbContextOptionsConfiguration<>).MakeGenericType(contextType);

        var toRemove = services
            .Where(d =>
                d.ServiceType == typeof(DbContextOptions<FlowmetryDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType == contextType ||
                d.ServiceType == optionsConfigType ||
                (d.ServiceType.IsGenericType &&
                 d.ServiceType.GetGenericArguments().Any(a => a == contextType)))
            .ToList();

        foreach (var d in toRemove)
            services.Remove(d);
    }

    public EventDispatchWebApplicationFactory WithSeededData()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowmetryDbContext>();
        db.Database.EnsureCreated();
        SeedCustomer(db);
        return this;
    }

    private static void SeedCustomer(FlowmetryDbContext db)
    {
        if (db.Customers.Any(c => c.Id == SeededCustomerId)) return;

        var customer = (Flowmetry.Domain.Customer)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(Flowmetry.Domain.Customer));

        typeof(Flowmetry.Domain.Customer).GetProperty("Id")!.SetValue(customer, SeededCustomerId);
        typeof(Flowmetry.Domain.Customer).GetProperty("Name")!.SetValue(customer, "Dispatch Test Customer");
        typeof(Flowmetry.Domain.Customer).GetProperty("Email")!.SetValue(customer, "dispatch@example.com");
        typeof(Flowmetry.Domain.Customer).GetProperty("RiskScore")!.SetValue(customer, 1);

        db.Customers.Add(customer);
        db.SaveChanges();
    }
}

// ── Tests ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Integration tests verifying that the DomainEventDispatchInterceptor fires
/// event handlers end-to-end via a real HTTP request through the test server.
/// Validates: Requirements 1.1, 2.1, 9.1
/// </summary>
public class DomainEventDispatchIntegrationTests : IClassFixture<EventDispatchWebApplicationFactory>
{
    private readonly EventDispatchWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DomainEventDispatchIntegrationTests(EventDispatchWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.WithSeededData();
        _client = TestAuthHelper.CreateAuthenticatedClient(factory.CreateClient());
    }

    [Fact]
    public async Task PostInvoice_DispatchesInvoiceCreatedEvent_SchedulerReceivesInitialReminderCall()
    {
        // Arrange
        var customerId = EventDispatchWebApplicationFactory.SeededCustomerId;
        var dueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30));

        var body = new
        {
            customerId,
            amount = 250m,
            dueDate = dueDate.ToString("yyyy-MM-dd")
        };

        // Act — POST triggers CreateInvoiceCommand → SaveChanges → interceptor → InvoiceCreatedHandler
        var response = await _client.PostAsJsonAsync("/api/invoices", body);

        // Assert HTTP response
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);

        // Assert the spy captured a ScheduleAsync call with ReminderType.Initial
        Assert.True(
            _factory.SpyScheduler.ScheduleCalls.Any(c => c.Type == ReminderType.Initial),
            "Expected IReminderScheduler.ScheduleAsync to be called with ReminderType.Initial after invoice creation.");
    }

    [Fact]
    public async Task PostInvoice_DispatchesInvoiceCreatedEvent_ScheduledAtIsCorrectOffset()
    {
        // Arrange
        var customerId = EventDispatchWebApplicationFactory.SeededCustomerId;
        var dueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(45));

        var body = new
        {
            customerId,
            amount = 400m,
            dueDate = dueDate.ToString("yyyy-MM-dd")
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/invoices", body);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);

        // Assert: ScheduledAt should be dueDate - InitialReminderDaysBeforeDue (default 7)
        var expectedScheduledAt = dueDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
            - TimeSpan.FromDays(7);

        var call = _factory.SpyScheduler.ScheduleCalls
            .FirstOrDefault(c => c.Type == ReminderType.Initial && c.ScheduledAt == expectedScheduledAt);

        Assert.True(
            call != default,
            $"Expected ScheduleAsync call with ScheduledAt={expectedScheduledAt} but got: [{string.Join(", ", _factory.SpyScheduler.ScheduleCalls.Select(c => $"({c.Type}, {c.ScheduledAt})"))}]");
    }
}

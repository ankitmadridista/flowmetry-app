using Flowmetry.Application.Common;
using Flowmetry.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flowmetry.Infrastructure.Events;

public class DomainEventDispatchInterceptor(
    IServiceScopeFactory scopeFactory,
    ILogger<DomainEventDispatchInterceptor> logger) : SaveChangesInterceptor
{
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await DispatchEventsAsync(eventData.Context, cancellationToken);
        return result;
    }

    private async Task DispatchEventsAsync(Microsoft.EntityFrameworkCore.DbContext? context, CancellationToken ct)
    {
        if (context is null) return;

        var invoicesWithEvents = context.ChangeTracker
            .Entries<Invoice>()
            .Select(e => e.Entity)
            .Where(i => i.DomainEvents.Count > 0)
            .ToList();

        if (invoicesWithEvents.Count == 0) return;

        // Collect all events before clearing, preserving per-invoice order
        var allEvents = new List<IDomainEvent>();
        foreach (var invoice in invoicesWithEvents)
        {
            var events = invoice.DomainEvents.ToList();
            invoice.ClearDomainEvents();
            allEvents.AddRange(events);
        }

        var batchCount = allEvents.Count;

        // Create a scope so scoped services (e.g. IReminderScheduler) can be resolved
        // by the MediatR handlers during publishing.
        using var scope = scopeFactory.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        foreach (var domainEvent in allEvents)
        {
            var notificationType = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
            var notification = (INotification)Activator.CreateInstance(notificationType, domainEvent)!;

            logger.LogDebug(
                "Publishing domain event {EventType} (batch count: {BatchCount})",
                domainEvent.GetType().Name,
                batchCount);

            await publisher.Publish(notification, ct);
        }
    }
}

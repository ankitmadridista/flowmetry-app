using Flowmetry.Application.Common;
using Flowmetry.Application.Invoices.Services;
using Flowmetry.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Flowmetry.Application.Invoices.EventHandlers;

public class InvoiceCreatedHandler(
    IReminderScheduler reminderScheduler,
    IOptions<ReminderOptions> options,
    ICashflowProjectionService cashflowProjectionService,
    ILogger<InvoiceCreatedHandler> logger)
    : INotificationHandler<DomainEventNotification<InvoiceCreated>>
{
    public async Task Handle(DomainEventNotification<InvoiceCreated> notification, CancellationToken cancellationToken)
    {
        var evt = notification.Event;
        logger.LogInformation("Handling InvoiceCreated for InvoiceId {InvoiceId}", evt.InvoiceId);

        var scheduledAt = evt.DueDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
            - TimeSpan.FromDays(options.Value.InitialReminderDaysBeforeDue);

        var result = await reminderScheduler.ScheduleAsync(evt.InvoiceId, ReminderType.Initial, scheduledAt, cancellationToken);

        if (result is ServiceResult.Failure failure)
        {
            logger.LogWarning(
                "Failed to schedule Initial reminder for InvoiceId {InvoiceId}: {Reason}",
                evt.InvoiceId,
                failure.Reason);
            return;
        }

        logger.LogInformation(
            "Scheduled Initial reminder for InvoiceId {InvoiceId} at {ScheduledAt}",
            evt.InvoiceId,
            scheduledAt);

        var cashflowResult = await cashflowProjectionService.InitialiseAsync(evt.InvoiceId, evt.Amount, cancellationToken);

        if (cashflowResult is ServiceResult.Failure cashflowFailure)
        {
            logger.LogWarning(
                "Failed to initialise cashflow projection for InvoiceId {InvoiceId}: {Reason}",
                evt.InvoiceId,
                cashflowFailure.Reason);
        }
    }
}

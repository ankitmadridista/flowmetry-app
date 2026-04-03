using Flowmetry.Application.Common;
using Flowmetry.Application.Invoices.Services;
using Flowmetry.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Flowmetry.Application.Invoices.EventHandlers;

public class InvoiceOverdueHandler(
    IAlertService alertService,
    IReminderScheduler reminderScheduler,
    IOptions<ReminderOptions> options,
    ICashflowProjectionService cashflowProjectionService,
    ILogger<InvoiceOverdueHandler> logger)
    : INotificationHandler<DomainEventNotification<InvoiceOverdue>>
{
    public async Task Handle(DomainEventNotification<InvoiceOverdue> notification, CancellationToken cancellationToken)
    {
        var evt = notification.Event;
        logger.LogInformation("Handling InvoiceOverdue for InvoiceId {InvoiceId}", evt.InvoiceId);

        var alertResult = await alertService.EmitOverdueAlertAsync(evt.InvoiceId, evt.DueDate, cancellationToken);

        if (alertResult is ServiceResult.Failure alertFailure)
        {
            logger.LogWarning(
                "Failed to emit overdue alert for InvoiceId {InvoiceId}: {Reason}",
                evt.InvoiceId,
                alertFailure.Reason);
        }
        else
        {
            logger.LogInformation("Emitted overdue alert for InvoiceId {InvoiceId}", evt.InvoiceId);
        }

        var scheduledAt = evt.DueDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
            + TimeSpan.FromDays(options.Value.EscalationReminderDaysAfterDue);

        var reminderResult = await reminderScheduler.CancelOrReplaceAsync(
            evt.InvoiceId, ReminderType.Escalation, scheduledAt, cancellationToken);

        if (reminderResult is ServiceResult.Failure reminderFailure)
        {
            logger.LogWarning(
                "Failed to schedule Escalation reminder for InvoiceId {InvoiceId}: {Reason}",
                evt.InvoiceId,
                reminderFailure.Reason);
        }
        else
        {
            logger.LogInformation(
                "Scheduled Escalation reminder for InvoiceId {InvoiceId} at {ScheduledAt}",
                evt.InvoiceId,
                scheduledAt);
        }

        var cashflowResult = await cashflowProjectionService.MarkOverdueAsync(evt.InvoiceId, cancellationToken);

        if (cashflowResult is ServiceResult.Failure cashflowFailure)
        {
            logger.LogWarning(
                "Failed to mark cashflow projection as overdue for InvoiceId {InvoiceId}: {Reason}",
                evt.InvoiceId,
                cashflowFailure.Reason);
        }
    }
}

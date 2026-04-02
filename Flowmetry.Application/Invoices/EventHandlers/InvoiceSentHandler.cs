using Flowmetry.Application.Common;
using Flowmetry.Application.Invoices.Services;
using Flowmetry.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Flowmetry.Application.Invoices.EventHandlers;

public class InvoiceSentHandler(
    IInvoiceRepository invoiceRepository,
    IReminderScheduler reminderScheduler,
    IOptions<ReminderOptions> options,
    ILogger<InvoiceSentHandler> logger)
    : INotificationHandler<DomainEventNotification<InvoiceSent>>
{
    public async Task Handle(DomainEventNotification<InvoiceSent> notification, CancellationToken cancellationToken)
    {
        var evt = notification.Event;
        logger.LogInformation("Handling InvoiceSent for InvoiceId {InvoiceId}", evt.InvoiceId);

        var invoice = await invoiceRepository.GetByIdAsync(evt.InvoiceId, cancellationToken);
        if (invoice is null)
        {
            logger.LogWarning("Invoice {InvoiceId} not found; skipping DueDate reminder scheduling", evt.InvoiceId);
            return;
        }

        var scheduledAt = invoice.DueDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
            - TimeSpan.FromDays(options.Value.DueDateReminderDaysBeforeDue);

        var result = await reminderScheduler.CancelOrReplaceAsync(evt.InvoiceId, ReminderType.DueDate, scheduledAt, cancellationToken);

        if (result is ServiceResult.Failure failure)
        {
            logger.LogWarning(
                "Failed to schedule DueDate reminder for InvoiceId {InvoiceId}: {Reason}",
                evt.InvoiceId,
                failure.Reason);
            return;
        }

        logger.LogInformation(
            "Scheduled DueDate reminder for InvoiceId {InvoiceId} at {ScheduledAt}",
            evt.InvoiceId,
            scheduledAt);
    }
}

using Flowmetry.Application.Common;
using Flowmetry.Application.Invoices;
using Flowmetry.Application.Reminders.Commands;
using Flowmetry.Domain;
using Flowmetry.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Flowmetry.Application.Invoices.EventHandlers;

public class InvoiceSentHandler(
    IInvoiceRepository invoiceRepository,
    ISender sender,
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
            logger.LogWarning("Invoice {InvoiceId} not found; skipping reminder scheduling", evt.InvoiceId);
            return;
        }

        var dueDate = invoice.DueDate;

        // Compute PreDue: DueDate - 3 days at 09:00 UTC
        var preDueAt = new DateTimeOffset(dueDate.Year, dueDate.Month, dueDate.Day, 9, 0, 0, TimeSpan.Zero)
            .AddDays(-3);

        if (preDueAt > DateTimeOffset.UtcNow)
        {
            await sender.Send(
                new ScheduleReminderCommand(invoice.Id, invoice.CustomerId, ReminderType.PreDue, preDueAt),
                cancellationToken);
        }
        else
        {
            logger.LogInformation(
                "Skipping PreDue reminder for InvoiceId {InvoiceId}: scheduled time {PreDueAt} is in the past",
                invoice.Id,
                preDueAt);
        }

        // PostDue: DueDate + 1 day at 09:00 UTC
        var postDueAt = new DateTimeOffset(dueDate.Year, dueDate.Month, dueDate.Day, 9, 0, 0, TimeSpan.Zero)
            .AddDays(1);

        await sender.Send(
            new ScheduleReminderCommand(invoice.Id, invoice.CustomerId, ReminderType.PostDue, postDueAt),
            cancellationToken);

        // Escalation: DueDate + 7 days at 09:00 UTC
        var escalationAt = new DateTimeOffset(dueDate.Year, dueDate.Month, dueDate.Day, 9, 0, 0, TimeSpan.Zero)
            .AddDays(7);

        await sender.Send(
            new ScheduleReminderCommand(invoice.Id, invoice.CustomerId, ReminderType.Escalation, escalationAt),
            cancellationToken);

        logger.LogInformation(
            "Scheduled reminders for InvoiceId {InvoiceId}: PostDue at {PostDueAt}, Escalation at {EscalationAt}",
            invoice.Id,
            postDueAt,
            escalationAt);
    }
}

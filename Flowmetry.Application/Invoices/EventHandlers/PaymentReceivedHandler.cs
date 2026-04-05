using Flowmetry.Application.Common;
using Flowmetry.Application.Invoices.Services;
using Flowmetry.Application.Reminders;
using Flowmetry.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Flowmetry.Application.Invoices.EventHandlers;

public class PaymentReceivedHandler(
    IInvoiceRepository invoiceRepository,
    ICashflowProjectionService cashflowProjectionService,
    IReminderRepository reminderRepository,
    ILogger<PaymentReceivedHandler> logger)
    : INotificationHandler<DomainEventNotification<PaymentReceived>>
{
    public async Task Handle(DomainEventNotification<PaymentReceived> notification, CancellationToken cancellationToken)
    {
        var evt = notification.Event;
        logger.LogInformation(
            "Handling PaymentReceived for InvoiceId {InvoiceId}, PaymentAmount {PaymentAmount}",
            evt.InvoiceId,
            evt.PaymentAmount);

        var applyResult = await cashflowProjectionService.ApplyPaymentAsync(
            evt.InvoiceId, evt.PaymentAmount, evt.RunningTotal, cancellationToken);

        if (applyResult is ServiceResult.Failure applyFailure)
        {
            logger.LogWarning(
                "Failed to apply payment to cashflow projection for InvoiceId {InvoiceId}, PaymentAmount {PaymentAmount}: {Reason}",
                evt.InvoiceId,
                evt.PaymentAmount,
                applyFailure.Reason);
            return;
        }

        var invoice = await invoiceRepository.GetByIdAsync(evt.InvoiceId, cancellationToken);
        if (invoice is not null && evt.RunningTotal == invoice.Amount)
        {
            var settleResult = await cashflowProjectionService.MarkSettledAsync(evt.InvoiceId, cancellationToken);

            if (settleResult is ServiceResult.Failure settleFailure)
            {
                logger.LogWarning(
                    "Failed to mark cashflow projection as settled for InvoiceId {InvoiceId}: {Reason}",
                    evt.InvoiceId,
                    settleFailure.Reason);
                return;
            }

            logger.LogInformation(
                "Cashflow projection marked as settled for InvoiceId {InvoiceId}",
                evt.InvoiceId);

            var pendingReminders = await reminderRepository.GetPendingByInvoiceIdAsync(invoice.Id, cancellationToken);
            foreach (var reminder in pendingReminders)
                reminder.Cancel();

            if (pendingReminders.Count > 0)
            {
                await reminderRepository.SaveChangesAsync(cancellationToken);
                logger.LogInformation(
                    "Cancelled {Count} pending reminder(s) for InvoiceId {InvoiceId}",
                    pendingReminders.Count,
                    invoice.Id);
            }

            return;
        }

        logger.LogInformation(
            "Cashflow projection updated for InvoiceId {InvoiceId}, RunningTotal {RunningTotal}",
            evt.InvoiceId,
            evt.RunningTotal);
    }
}

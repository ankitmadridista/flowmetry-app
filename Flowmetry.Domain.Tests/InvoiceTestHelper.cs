using Flowmetry.Domain;

namespace Flowmetry.Domain.Tests;

internal static class InvoiceTestHelper
{
    private static readonly Guid DefaultCustomerId = Guid.NewGuid();
    private static readonly DateOnly DefaultDueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30));

    public static Invoice CreateDraftInvoice(
        Guid? customerId = null,
        decimal amount = 100m,
        DateOnly? dueDate = null)
    {
        return Invoice.Create(
            customerId ?? DefaultCustomerId,
            amount,
            dueDate ?? DefaultDueDate);
    }

    public static Invoice CreateSentInvoice(decimal amount = 100m)
    {
        var invoice = CreateDraftInvoice(amount: amount);
        invoice.MarkAsSent();
        invoice.ClearDomainEvents();
        return invoice;
    }

    public static Invoice CreatePartiallyPaidInvoice(decimal invoiceAmount = 100m, decimal paymentAmount = 50m)
    {
        var invoice = CreateSentInvoice(amount: invoiceAmount);
        invoice.RecordPayment(paymentAmount);
        invoice.ClearDomainEvents();
        return invoice;
    }

    public static Invoice CreatePaidInvoice(decimal amount = 100m)
    {
        var invoice = CreateSentInvoice(amount: amount);
        invoice.RecordPayment(amount);
        invoice.ClearDomainEvents();
        return invoice;
    }

    public static Invoice CreateOverdueInvoice(decimal amount = 100m)
    {
        var invoice = CreateSentInvoice(amount: amount);
        invoice.MarkOverdue();
        invoice.ClearDomainEvents();
        return invoice;
    }
}

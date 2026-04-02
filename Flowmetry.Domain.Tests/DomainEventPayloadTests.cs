using Flowmetry.Domain;
using Flowmetry.Domain.Events;

namespace Flowmetry.Domain.Tests;

public class DomainEventPayloadTests
{
    [Fact]
    public void InvoiceCreated_ShouldContainCorrectPayload()
    {
        var customerId = Guid.NewGuid();
        var amount = 500m;
        var dueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(14));

        var invoice = Invoice.Create(customerId, amount, dueDate);

        var evt = Assert.Single(invoice.DomainEvents);
        var created = Assert.IsType<InvoiceCreated>(evt);

        Assert.Equal(invoice.Id, created.InvoiceId);
        Assert.Equal(customerId, created.CustomerId);
        Assert.Equal(amount, created.Amount);
        Assert.Equal(dueDate, created.DueDate);
    }

    [Fact]
    public void InvoiceSent_ShouldContainCorrectPayload()
    {
        var invoice = InvoiceTestHelper.CreateDraftInvoice();
        invoice.ClearDomainEvents();

        var before = DateTimeOffset.UtcNow;
        invoice.MarkAsSent();
        var after = DateTimeOffset.UtcNow;

        var evt = Assert.Single(invoice.DomainEvents);
        var sent = Assert.IsType<InvoiceSent>(evt);

        Assert.Equal(invoice.Id, sent.InvoiceId);
        Assert.NotEqual(default, sent.SentAt);
        Assert.True(sent.SentAt >= before && sent.SentAt <= after);
    }

    [Fact]
    public void PaymentReceived_ShouldContainCorrectPayload()
    {
        var invoice = InvoiceTestHelper.CreateSentInvoice(amount: 200m);
        invoice.RecordPayment(80m);
        invoice.ClearDomainEvents();

        invoice.RecordPayment(70m);

        var evt = Assert.Single(invoice.DomainEvents);
        var payment = Assert.IsType<PaymentReceived>(evt);

        Assert.Equal(invoice.Id, payment.InvoiceId);
        Assert.Equal(70m, payment.PaymentAmount);
        Assert.Equal(150m, payment.RunningTotal);
    }

    [Fact]
    public void InvoiceOverdue_ShouldContainCorrectPayload()
    {
        var dueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7));
        var invoice = InvoiceTestHelper.CreateDraftInvoice(dueDate: dueDate);
        invoice.MarkAsSent();
        invoice.ClearDomainEvents();

        invoice.MarkOverdue();

        var evt = Assert.Single(invoice.DomainEvents);
        var overdue = Assert.IsType<InvoiceOverdue>(evt);

        Assert.Equal(invoice.Id, overdue.InvoiceId);
        Assert.Equal(dueDate, overdue.DueDate);
    }
}

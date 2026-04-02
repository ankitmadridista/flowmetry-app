using Flowmetry.Domain;
using Flowmetry.Domain.Events;

namespace Flowmetry.Domain.Tests;

public class InvoiceMarkAsSentTests
{
    [Fact]
    public void MarkAsSent_FromDraft_ShouldTransitionToSent()
    {
        var invoice = InvoiceTestHelper.CreateDraftInvoice();

        invoice.MarkAsSent();

        Assert.Equal(InvoiceStatus.Sent, invoice.Status);
    }

    [Fact]
    public void MarkAsSent_FromDraft_ShouldRaiseInvoiceSentEvent()
    {
        var invoice = InvoiceTestHelper.CreateDraftInvoice();
        invoice.ClearDomainEvents();

        invoice.MarkAsSent();

        var evt = Assert.Single(invoice.DomainEvents);
        Assert.IsType<InvoiceSent>(evt);
    }

    [Fact]
    public void MarkAsSent_FromSent_ShouldThrowInvalidOperationException()
    {
        var invoice = InvoiceTestHelper.CreateSentInvoice();

        Assert.Throws<InvalidOperationException>(() => invoice.MarkAsSent());
    }

    [Fact]
    public void MarkAsSent_FromPaid_ShouldThrowInvalidOperationException()
    {
        var invoice = InvoiceTestHelper.CreatePaidInvoice();

        Assert.Throws<InvalidOperationException>(() => invoice.MarkAsSent());
    }

    [Fact]
    public void MarkAsSent_FromOverdue_ShouldThrowInvalidOperationException()
    {
        var invoice = InvoiceTestHelper.CreateOverdueInvoice();

        Assert.Throws<InvalidOperationException>(() => invoice.MarkAsSent());
    }
}

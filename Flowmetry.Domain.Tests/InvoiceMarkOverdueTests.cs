using Flowmetry.Domain;
using Flowmetry.Domain.Events;

namespace Flowmetry.Domain.Tests;

public class InvoiceMarkOverdueTests
{
    [Fact]
    public void MarkOverdue_FromSent_ShouldTransitionToOverdue()
    {
        var invoice = InvoiceTestHelper.CreateSentInvoice();

        invoice.MarkOverdue();

        Assert.Equal(InvoiceStatus.Overdue, invoice.Status);
    }

    [Fact]
    public void MarkOverdue_FromPartiallyPaid_ShouldTransitionToOverdue()
    {
        var invoice = InvoiceTestHelper.CreatePartiallyPaidInvoice();

        invoice.MarkOverdue();

        Assert.Equal(InvoiceStatus.Overdue, invoice.Status);
    }

    [Fact]
    public void MarkOverdue_FromDraft_ShouldThrowInvalidOperationException()
    {
        var invoice = InvoiceTestHelper.CreateDraftInvoice();

        Assert.Throws<InvalidOperationException>(() => invoice.MarkOverdue());
    }

    [Fact]
    public void MarkOverdue_FromPaid_ShouldThrowInvalidOperationException()
    {
        var invoice = InvoiceTestHelper.CreatePaidInvoice();

        Assert.Throws<InvalidOperationException>(() => invoice.MarkOverdue());
    }

    [Fact]
    public void MarkOverdue_ShouldRaiseInvoiceOverdueEvent()
    {
        var invoice = InvoiceTestHelper.CreateSentInvoice();

        invoice.MarkOverdue();

        var evt = Assert.Single(invoice.DomainEvents);
        Assert.IsType<InvoiceOverdue>(evt);
    }
}

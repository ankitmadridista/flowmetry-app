using Flowmetry.Domain;

namespace Flowmetry.Domain.Tests;

public class InvoiceRecordPaymentTests
{
    [Fact]
    public void RecordPayment_PartialPayment_ShouldTransitionToPartiallyPaid()
    {
        var invoice = InvoiceTestHelper.CreateSentInvoice(amount: 100m);

        invoice.RecordPayment(60m);

        Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
    }

    [Fact]
    public void RecordPayment_FullPayment_ShouldTransitionToPaid()
    {
        var invoice = InvoiceTestHelper.CreateSentInvoice(amount: 100m);

        invoice.RecordPayment(100m);

        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
    }

    [Fact]
    public void RecordPayment_MultiplePayments_ShouldAccumulate()
    {
        var invoice = InvoiceTestHelper.CreateSentInvoice(amount: 100m);

        invoice.RecordPayment(40m);
        invoice.RecordPayment(35m);
        invoice.RecordPayment(25m);

        Assert.Equal(3, invoice.Payments.Count);
        Assert.Equal(100m, invoice.Payments.Sum(p => p.Amount));
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
    }

    [Fact]
    public void RecordPayment_ZeroAmount_ShouldThrowArgumentException()
    {
        var invoice = InvoiceTestHelper.CreateSentInvoice();

        Assert.Throws<ArgumentException>(() => invoice.RecordPayment(0m));
    }

    [Fact]
    public void RecordPayment_NegativeAmount_ShouldThrowArgumentException()
    {
        var invoice = InvoiceTestHelper.CreateSentInvoice();

        Assert.Throws<ArgumentException>(() => invoice.RecordPayment(-10m));
    }

    [Fact]
    public void RecordPayment_Overpayment_ShouldThrowInvalidOperationException()
    {
        var invoice = InvoiceTestHelper.CreateSentInvoice(amount: 100m);

        Assert.Throws<InvalidOperationException>(() => invoice.RecordPayment(150m));
    }

    [Fact]
    public void RecordPayment_FromDraft_ShouldThrowInvalidOperationException()
    {
        var invoice = InvoiceTestHelper.CreateDraftInvoice();

        Assert.Throws<InvalidOperationException>(() => invoice.RecordPayment(50m));
    }

    [Fact]
    public void RecordPayment_FromPaid_ShouldThrowInvalidOperationException()
    {
        var invoice = InvoiceTestHelper.CreatePaidInvoice(amount: 100m);

        Assert.Throws<InvalidOperationException>(() => invoice.RecordPayment(10m));
    }
}

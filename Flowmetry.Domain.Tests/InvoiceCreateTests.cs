using Flowmetry.Domain;
using Flowmetry.Domain.Events;

namespace Flowmetry.Domain.Tests;

public class InvoiceCreateTests
{
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly decimal _amount = 250m;
    private readonly DateOnly _dueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30));

    [Fact]
    public void Create_ShouldReturnDraftStatus()
    {
        var invoice = Invoice.Create(_customerId, _amount, _dueDate);

        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
    }

    [Fact]
    public void Create_ShouldHaveEmptyPayments()
    {
        var invoice = Invoice.Create(_customerId, _amount, _dueDate);

        Assert.Empty(invoice.Payments);
    }

    [Fact]
    public void Create_ShouldRaiseInvoiceCreatedEvent()
    {
        var invoice = Invoice.Create(_customerId, _amount, _dueDate);

        var evt = Assert.Single(invoice.DomainEvents);
        Assert.IsType<InvoiceCreated>(evt);
    }

    [Fact]
    public void Create_ShouldSetCorrectProperties()
    {
        var invoice = Invoice.Create(_customerId, _amount, _dueDate);

        Assert.NotEqual(Guid.Empty, invoice.Id);
        Assert.Equal(_customerId, invoice.CustomerId);
        Assert.Equal(_amount, invoice.Amount);
        Assert.Equal(_dueDate, invoice.DueDate);
    }
}

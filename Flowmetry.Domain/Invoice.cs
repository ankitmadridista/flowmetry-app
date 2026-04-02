using Flowmetry.Domain.Events;

namespace Flowmetry.Domain;

public class Invoice
{
    private readonly List<Payment> _payments = new();
    private readonly List<IDomainEvent> _domainEvents = new();

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public decimal Amount { get; private set; }
    public DateOnly DueDate { get; private set; }
    public InvoiceStatus Status { get; private set; }
    public IReadOnlyList<Payment> Payments => _payments.AsReadOnly();
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    // Required by EF Core
    private Invoice() { }

    public static Invoice Create(Guid customerId, decimal amount, DateOnly dueDate)
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Amount = amount,
            DueDate = dueDate,
            Status = InvoiceStatus.Draft
        };

        invoice._domainEvents.Add(new InvoiceCreated(invoice.Id, customerId, amount, dueDate));

        return invoice;
    }

    public void MarkAsSent()
    {
        if (Status != InvoiceStatus.Draft)
            throw new InvalidOperationException("Invoice must be in Draft status to be sent.");

        Status = InvoiceStatus.Sent;
        _domainEvents.Add(new InvoiceSent(Id, DateTimeOffset.UtcNow));
    }

    public void RecordPayment(decimal amount)
    {
        if (Status != InvoiceStatus.Sent && Status != InvoiceStatus.PartiallyPaid)
            throw new InvalidOperationException("Invoice must be in Sent or PartiallyPaid status to record a payment.");

        if (amount <= 0)
            throw new ArgumentException("Payment amount must be greater than zero.", nameof(amount));

        if (_payments.Sum(p => p.Amount) + amount > Amount)
            throw new InvalidOperationException("Payment would exceed the invoice amount.");

        _payments.Add(Payment.Create(Id, amount));

        var total = _payments.Sum(p => p.Amount);
        _domainEvents.Add(new PaymentReceived(Id, amount, total));

        Status = (total == Amount) ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
    }

    public void MarkOverdue()
    {
        if (Status != InvoiceStatus.Sent && Status != InvoiceStatus.PartiallyPaid)
            throw new InvalidOperationException("Invoice must be in Sent or PartiallyPaid status to be marked overdue.");

        Status = InvoiceStatus.Overdue;
        _domainEvents.Add(new InvoiceOverdue(Id, DueDate));
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

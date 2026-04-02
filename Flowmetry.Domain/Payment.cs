namespace Flowmetry.Domain;

public class Payment
{
    public Guid Id { get; private set; }
    public Guid InvoiceId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }

    // Required by EF Core
    private Payment() { }

    internal static Payment Create(Guid invoiceId, decimal amount)
    {
        return new Payment
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoiceId,
            Amount = amount,
            RecordedAt = DateTimeOffset.UtcNow
        };
    }
}

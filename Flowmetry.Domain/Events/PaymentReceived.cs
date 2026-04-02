namespace Flowmetry.Domain.Events;

public record PaymentReceived(Guid InvoiceId, decimal PaymentAmount, decimal RunningTotal) : IDomainEvent;

namespace Flowmetry.Domain.Events;

public record InvoiceCreated(Guid InvoiceId, Guid CustomerId, decimal Amount, DateOnly DueDate) : IDomainEvent;

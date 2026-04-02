namespace Flowmetry.Domain.Events;

public record InvoiceOverdue(Guid InvoiceId, DateOnly DueDate) : IDomainEvent;

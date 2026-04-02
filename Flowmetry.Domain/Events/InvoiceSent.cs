namespace Flowmetry.Domain.Events;

public record InvoiceSent(Guid InvoiceId, DateTimeOffset SentAt) : IDomainEvent;

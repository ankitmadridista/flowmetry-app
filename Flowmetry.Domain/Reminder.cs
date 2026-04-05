namespace Flowmetry.Domain;

public class Reminder
{
    public Guid Id { get; private set; }
    public Guid InvoiceId { get; private set; }
    public Guid CustomerId { get; private set; }
    public ReminderType ReminderType { get; private set; }
    public DateTimeOffset ScheduledAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public ReminderStatus Status { get; private set; }

    // Required by EF Core
    private Reminder() { }

    public static Reminder Create(Guid invoiceId, Guid customerId, ReminderType type, DateTimeOffset scheduledAt)
    {
        return new Reminder
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoiceId,
            CustomerId = customerId,
            ReminderType = type,
            ScheduledAt = scheduledAt,
            Status = ReminderStatus.Pending
        };
    }

    public void MarkAsSent()
    {
        if (Status == ReminderStatus.Sent || Status == ReminderStatus.Cancelled)
            throw new InvalidOperationException($"Cannot mark a reminder as sent when it is already {Status}.");

        Status = ReminderStatus.Sent;
        SentAt = DateTimeOffset.UtcNow;
    }

    public void Cancel()
    {
        if (Status == ReminderStatus.Cancelled)
            return;

        Status = ReminderStatus.Cancelled;
    }
}

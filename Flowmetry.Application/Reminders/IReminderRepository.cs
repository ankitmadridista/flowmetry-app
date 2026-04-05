using Flowmetry.Domain;

namespace Flowmetry.Application.Reminders;

public interface IReminderRepository
{
    Task AddAsync(Reminder reminder, CancellationToken ct = default);
    Task<IReadOnlyList<Reminder>> GetPendingDueAsync(DateTimeOffset utcNow, CancellationToken ct = default);
    Task<IReadOnlyList<Reminder>> GetByInvoiceIdAsync(Guid invoiceId, CancellationToken ct = default);
    Task<IReadOnlyList<Reminder>> GetPendingByInvoiceIdAsync(Guid invoiceId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

using Flowmetry.Application.Reminders;
using Flowmetry.Domain;
using Microsoft.EntityFrameworkCore;

namespace Flowmetry.Infrastructure.Persistence;

public class ReminderRepository(FlowmetryDbContext dbContext) : IReminderRepository
{
    public async Task AddAsync(Reminder reminder, CancellationToken ct = default)
        => await dbContext.Reminders.AddAsync(reminder, ct);

    public async Task<IReadOnlyList<Reminder>> GetPendingDueAsync(DateTimeOffset utcNow, CancellationToken ct = default)
        => await dbContext.Reminders
            .Where(r => r.Status == ReminderStatus.Pending && r.ScheduledAt <= utcNow)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Reminder>> GetByInvoiceIdAsync(Guid invoiceId, CancellationToken ct = default)
        => await dbContext.Reminders
            .Where(r => r.InvoiceId == invoiceId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Reminder>> GetPendingByInvoiceIdAsync(Guid invoiceId, CancellationToken ct = default)
        => await dbContext.Reminders
            .Where(r => r.InvoiceId == invoiceId && r.Status == ReminderStatus.Pending)
            .ToListAsync(ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await dbContext.SaveChangesAsync(ct);
}

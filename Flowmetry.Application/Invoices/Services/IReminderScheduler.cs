using Flowmetry.Application.Common;

namespace Flowmetry.Application.Invoices.Services;

public enum ReminderType { Initial, DueDate, Escalation }

public interface IReminderScheduler
{
    Task<ServiceResult> ScheduleAsync(Guid invoiceId, ReminderType type, DateTimeOffset scheduledAt, CancellationToken ct = default);
    Task<ServiceResult> CancelOrReplaceAsync(Guid invoiceId, ReminderType type, DateTimeOffset scheduledAt, CancellationToken ct = default);
}

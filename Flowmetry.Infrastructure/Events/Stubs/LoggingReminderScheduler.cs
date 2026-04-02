using Flowmetry.Application.Common;
using Flowmetry.Application.Invoices.Services;
using Microsoft.Extensions.Logging;

namespace Flowmetry.Infrastructure.Events.Stubs;

public class LoggingReminderScheduler(ILogger<LoggingReminderScheduler> logger) : IReminderScheduler
{
    public Task<ServiceResult> ScheduleAsync(Guid invoiceId, ReminderType type, DateTimeOffset scheduledAt, CancellationToken ct = default)
    {
        logger.LogInformation("ScheduleAsync called: InvoiceId={InvoiceId}, Type={Type}, ScheduledAt={ScheduledAt}", invoiceId, type, scheduledAt);
        return Task.FromResult<ServiceResult>(new ServiceResult.Success());
    }

    public Task<ServiceResult> CancelOrReplaceAsync(Guid invoiceId, ReminderType type, DateTimeOffset scheduledAt, CancellationToken ct = default)
    {
        logger.LogInformation("CancelOrReplaceAsync called: InvoiceId={InvoiceId}, Type={Type}, ScheduledAt={ScheduledAt}", invoiceId, type, scheduledAt);
        return Task.FromResult<ServiceResult>(new ServiceResult.Success());
    }
}

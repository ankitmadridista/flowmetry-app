using Flowmetry.Application.Common;
using Flowmetry.Application.Invoices.Services;
using Microsoft.Extensions.Logging;

namespace Flowmetry.Infrastructure.Events.Stubs;

public class LoggingAlertService(ILogger<LoggingAlertService> logger) : IAlertService
{
    public Task<ServiceResult> EmitOverdueAlertAsync(Guid invoiceId, DateOnly dueDate, CancellationToken ct)
    {
        logger.LogInformation("EmitOverdueAlertAsync called: InvoiceId={InvoiceId}, DueDate={DueDate}", invoiceId, dueDate);
        return Task.FromResult<ServiceResult>(new ServiceResult.Success());
    }
}

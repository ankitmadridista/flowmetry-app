using Flowmetry.Application.Common;
using Flowmetry.Application.Invoices.Services;
using Microsoft.Extensions.Logging;

namespace Flowmetry.Infrastructure.Events.Stubs;

public class LoggingCashflowProjectionService(ILogger<LoggingCashflowProjectionService> logger) : ICashflowProjectionService
{
    public Task<ServiceResult> InitialiseAsync(Guid invoiceId, decimal invoiceAmount, CancellationToken ct)
    {
        logger.LogInformation("InitialiseAsync called: InvoiceId={InvoiceId}, InvoiceAmount={InvoiceAmount}", invoiceId, invoiceAmount);
        return Task.FromResult<ServiceResult>(new ServiceResult.Success());
    }

    public Task<ServiceResult> ApplyPaymentAsync(Guid invoiceId, decimal paymentAmount, decimal runningTotal, CancellationToken ct)
    {
        logger.LogInformation("ApplyPaymentAsync called: InvoiceId={InvoiceId}, PaymentAmount={PaymentAmount}, RunningTotal={RunningTotal}", invoiceId, paymentAmount, runningTotal);
        return Task.FromResult<ServiceResult>(new ServiceResult.Success());
    }

    public Task<ServiceResult> MarkSettledAsync(Guid invoiceId, CancellationToken ct)
    {
        logger.LogInformation("MarkSettledAsync called: InvoiceId={InvoiceId}", invoiceId);
        return Task.FromResult<ServiceResult>(new ServiceResult.Success());
    }

    public Task<ServiceResult> MarkOverdueAsync(Guid invoiceId, CancellationToken ct)
    {
        logger.LogInformation("MarkOverdueAsync called: InvoiceId={InvoiceId}", invoiceId);
        return Task.FromResult<ServiceResult>(new ServiceResult.Success());
    }
}

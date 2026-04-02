using Flowmetry.Application.Common;

namespace Flowmetry.Application.Invoices.Services;

public interface ICashflowProjectionService
{
    Task<ServiceResult> ApplyPaymentAsync(Guid invoiceId, decimal paymentAmount, decimal runningTotal, CancellationToken ct);
    Task<ServiceResult> MarkSettledAsync(Guid invoiceId, CancellationToken ct);
}

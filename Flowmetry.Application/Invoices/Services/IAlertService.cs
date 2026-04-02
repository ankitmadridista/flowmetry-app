using Flowmetry.Application.Common;

namespace Flowmetry.Application.Invoices.Services;

public interface IAlertService
{
    Task<ServiceResult> EmitOverdueAlertAsync(Guid invoiceId, DateOnly dueDate, CancellationToken ct);
}

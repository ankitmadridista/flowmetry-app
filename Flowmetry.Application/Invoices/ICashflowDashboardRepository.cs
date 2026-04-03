using Flowmetry.Application.Invoices.Dtos;

namespace Flowmetry.Application.Invoices;

public interface ICashflowDashboardRepository
{
    Task<CashflowSummary> GetSummaryAsync(CancellationToken ct);
}

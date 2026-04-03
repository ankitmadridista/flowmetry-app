using Flowmetry.Application.Common;
using Flowmetry.Application.Invoices.Dtos;
using MediatR;

namespace Flowmetry.Application.Invoices.Queries;

public record GetCashflowDashboardQuery : IRequest<Result<CashflowSummary>>;

public class GetCashflowDashboardQueryHandler(ICashflowDashboardRepository repository)
    : IRequestHandler<GetCashflowDashboardQuery, Result<CashflowSummary>>
{
    public async Task<Result<CashflowSummary>> Handle(GetCashflowDashboardQuery request, CancellationToken ct)
    {
        var summary = await repository.GetSummaryAsync(ct);
        return new Result<CashflowSummary>.Success(summary);
    }
}

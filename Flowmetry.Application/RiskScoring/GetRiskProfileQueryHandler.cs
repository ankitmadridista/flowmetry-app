using Flowmetry.Application.Common;
using Flowmetry.Domain;
using MediatR;

namespace Flowmetry.Application.RiskScoring;

public class GetRiskProfileQueryHandler(IRiskProfileRepository repository)
    : IRequestHandler<GetRiskProfileQuery, Result<RiskProfileDto>>
{
    public async Task<Result<RiskProfileDto>> Handle(
        GetRiskProfileQuery request,
        CancellationToken cancellationToken)
    {
        if (!await repository.CustomerExistsAsync(request.CustomerId, cancellationToken))
            return new Result<RiskProfileDto>.NotFound(
                $"Customer '{request.CustomerId}' not found.");

        var invoices = await repository.GetInvoicesWithPaymentsByCustomerAsync(
            request.CustomerId, cancellationToken);

        var profile = RiskScoreCalculator.Calculate(invoices);

        return new Result<RiskProfileDto>.Success(new RiskProfileDto(
            profile.RiskScore,
            profile.RiskBand.ToString(),
            profile.TotalInvoices,
            profile.OverdueCount,
            profile.PartiallyPaidCount,
            profile.LatePaymentCount,
            profile.AverageDaysLate));
    }
}

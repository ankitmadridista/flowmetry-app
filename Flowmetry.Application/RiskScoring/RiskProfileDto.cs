using Flowmetry.Application.Common;
using MediatR;

namespace Flowmetry.Application.RiskScoring;

public record RiskProfileDto(
    int RiskScore,
    string RiskBand,
    int TotalInvoices,
    int OverdueCount,
    int PartiallyPaidCount,
    int LatePaymentCount,
    double AverageDaysLate);

public record GetRiskProfileQuery(Guid CustomerId)
    : IRequest<Result<RiskProfileDto>>;

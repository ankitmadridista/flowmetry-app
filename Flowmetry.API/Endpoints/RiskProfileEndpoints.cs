using Flowmetry.Application.Common;
using Flowmetry.Application.RiskScoring;
using MediatR;

namespace Flowmetry.API.Endpoints;

public static class RiskProfileEndpoints
{
    public static IEndpointRouteBuilder MapRiskProfileEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/customers/{id:guid}/risk-profile",
            async (Guid id, IMediator mediator) =>
            {
                var result = await mediator.Send(new GetRiskProfileQuery(id));
                return result switch
                {
                    Result<RiskProfileDto>.Success s =>
                        Results.Ok(s.Value),
                    Result<RiskProfileDto>.NotFound n =>
                        Results.NotFound(new { message = n.Message }),
                    _ => Results.StatusCode(500)
                };
            });

        return app;
    }
}

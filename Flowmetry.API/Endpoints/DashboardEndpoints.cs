using Flowmetry.Application.Invoices.Dtos;
using Flowmetry.Application.Invoices.Queries;
using Flowmetry.Application.Common;
using MediatR;

namespace Flowmetry.API.Endpoints;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/dashboard/cashflow
        app.MapGet("/api/dashboard/cashflow", async (IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetCashflowDashboardQuery(), ct);
            return result switch
            {
                Result<CashflowSummary>.Success s => Results.Ok(s.Value),
                _ => Results.StatusCode(500)
            };
        }).RequireAuthorization();

        return app;
    }
}

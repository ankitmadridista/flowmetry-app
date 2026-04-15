using Flowmetry.Application.Common;
using Flowmetry.Application.Customers.Commands;
using Flowmetry.Application.Customers.Dtos;
using Flowmetry.Application.Customers.Queries;
using MediatR;

namespace Flowmetry.API.Endpoints;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers").RequireAuthorization();

        // POST /api/customers
        group.MapPost("", async (CreateCustomerRequest request, IMediator mediator) =>
        {
            var command = new CreateCustomerCommand(request.Name, request.Email);
            var result = await mediator.Send(command);
            return result switch
            {
                Result<Guid>.Success s             => Results.Created($"/api/customers/{s.Value}", new { id = s.Value }),
                Result<Guid>.ValidationFailure f   => Results.BadRequest(new { errors = f.Errors }),
                Result<Guid>.Conflict c            => Results.Conflict(new { message = c.Message }),
                _                                  => Results.StatusCode(500)
            };
        });

        // GET /api/customers
        group.MapGet("", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetAllCustomersQuery());
            return result switch
            {
                Result<IReadOnlyList<CustomerSummaryDto>>.Success s => Results.Ok(s.Value),
                _ => Results.StatusCode(500)
            };
        });

        // GET /api/customers/{id}
        group.MapGet("{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetCustomerQuery(id));
            return result switch
            {
                Result<CustomerSummaryDto>.Success s   => Results.Ok(s.Value),
                Result<CustomerSummaryDto>.NotFound n  => Results.NotFound(new { message = n.Message }),
                _ => Results.StatusCode(500)
            };
        });

        // GET /api/customers/{id}/invoices
        group.MapGet("{id:guid}/invoices", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetCustomerInvoicesQuery(id));
            return result switch
            {
                Result<IReadOnlyList<CustomerInvoiceSummaryDto>>.Success s  => Results.Ok(s.Value),
                Result<IReadOnlyList<CustomerInvoiceSummaryDto>>.NotFound n => Results.NotFound(new { message = n.Message }),
                _ => Results.StatusCode(500)
            };
        });

        return app;
    }
}

public record CreateCustomerRequest(string Name, string Email);

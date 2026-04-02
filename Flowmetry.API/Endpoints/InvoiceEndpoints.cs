using Flowmetry.Application.Common;
using Flowmetry.Application.Invoices.Commands;
using Flowmetry.Application.Invoices.Dtos;
using Flowmetry.Application.Invoices.Queries;
using MediatR;

namespace Flowmetry.API.Endpoints;

public static class InvoiceEndpoints
{
    public static IEndpointRouteBuilder MapInvoiceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/invoices");

        // POST /api/invoices
        group.MapPost("", async (CreateInvoiceRequest request, IMediator mediator) =>
        {
            var command = new CreateInvoiceCommand(request.CustomerId, request.Amount, request.DueDate);
            var result = await mediator.Send(command);
            return result switch
            {
                Result<Guid>.Success s => Results.Created($"/api/invoices/{s.Value}", new { id = s.Value }),
                Result<Guid>.ValidationFailure f => Results.BadRequest(new { errors = f.Errors }),
                Result<Guid>.NotFound n => Results.NotFound(new { message = n.Message }),
                _ => Results.StatusCode(500)
            };
        });

        // POST /api/invoices/{id}/send
        group.MapPost("{id:guid}/send", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new SendInvoiceCommand(id));
            return result switch
            {
                Result<Unit>.Success => Results.Ok(),
                Result<Unit>.NotFound n => Results.NotFound(new { message = n.Message }),
                _ => Results.StatusCode(500)
            };
        });

        // POST /api/invoices/{id}/payments
        group.MapPost("{id:guid}/payments", async (Guid id, RecordPaymentRequest request, IMediator mediator) =>
        {
            var command = new RecordPaymentCommand(id, request.Amount);
            var result = await mediator.Send(command);
            return result switch
            {
                Result<Unit>.Success => Results.Ok(),
                Result<Unit>.ValidationFailure f => Results.BadRequest(new { errors = f.Errors }),
                Result<Unit>.NotFound n => Results.NotFound(new { message = n.Message }),
                _ => Results.StatusCode(500)
            };
        });

        // GET /api/invoices/{id}
        group.MapGet("{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var query = new GetInvoiceDetailsQuery(id);
            var result = await mediator.Send(query);
            return result switch
            {
                Result<InvoiceDetailsDto>.Success s => Results.Ok(s.Value),
                Result<InvoiceDetailsDto>.NotFound n => Results.NotFound(new { message = n.Message }),
                _ => Results.StatusCode(500)
            };
        });

        // GET /api/invoices
        group.MapGet("", async (bool? overdue, IMediator mediator) =>
        {
            var query = new GetOverdueInvoicesQuery(overdue ?? true);
            var result = await mediator.Send(query);
            return result switch
            {
                Result<IReadOnlyList<InvoiceSummaryDto>>.Success s => Results.Ok(s.Value),
                _ => Results.StatusCode(500)
            };
        });

        return app;
    }
}

// Request DTOs
public record CreateInvoiceRequest(Guid CustomerId, decimal Amount, DateOnly DueDate);
public record RecordPaymentRequest(decimal Amount);

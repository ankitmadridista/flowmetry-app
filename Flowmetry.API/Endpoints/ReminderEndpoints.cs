using Flowmetry.Application.Common;
using Flowmetry.Application.Reminders.Queries;
using MediatR;

namespace Flowmetry.API.Endpoints;

public static class ReminderEndpoints
{
    public static IEndpointRouteBuilder MapReminderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/invoices");

        // GET /api/invoices/{invoiceId}/reminders
        group.MapGet("{invoiceId:guid}/reminders", async (Guid invoiceId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetRemindersForInvoiceQuery(invoiceId));
            return result switch
            {
                Result<IReadOnlyList<ReminderDto>>.Success s => Results.Ok(s.Value),
                Result<IReadOnlyList<ReminderDto>>.NotFound n => Results.NotFound(new { message = n.Message }),
                _ => Results.StatusCode(500)
            };
        });

        return app;
    }
}

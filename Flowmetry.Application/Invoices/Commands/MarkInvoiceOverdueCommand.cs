using Flowmetry.Application.Common;
using MediatR;

namespace Flowmetry.Application.Invoices.Commands;

public record MarkInvoiceOverdueCommand(Guid InvoiceId) : IRequest<Result<Unit>>;

public class MarkInvoiceOverdueCommandHandler(IInvoiceRepository repository)
    : IRequestHandler<MarkInvoiceOverdueCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(MarkInvoiceOverdueCommand request, CancellationToken cancellationToken)
    {
        var invoice = await repository.GetByIdAsync(request.InvoiceId, cancellationToken);
        if (invoice is null)
            return new Result<Unit>.NotFound($"Invoice '{request.InvoiceId}' not found.");

        invoice.MarkOverdue();

        await repository.SaveChangesAsync(cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}

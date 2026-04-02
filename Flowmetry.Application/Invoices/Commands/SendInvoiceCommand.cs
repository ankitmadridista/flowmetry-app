using Flowmetry.Application.Common;
using MediatR;

namespace Flowmetry.Application.Invoices.Commands;

public record SendInvoiceCommand(Guid InvoiceId) : IRequest<Result<Unit>>;

public class SendInvoiceCommandHandler(IInvoiceRepository repository)
    : IRequestHandler<SendInvoiceCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(SendInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await repository.GetByIdAsync(request.InvoiceId, cancellationToken);
        if (invoice is null)
            return new Result<Unit>.NotFound($"Invoice '{request.InvoiceId}' not found.");

        invoice.MarkAsSent();

        await repository.SaveChangesAsync(cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}

using Flowmetry.Application.Common;
using MediatR;

namespace Flowmetry.Application.Invoices.Commands;

public record RecordPaymentCommand(Guid InvoiceId, decimal Amount) : IRequest<Result<Unit>>;

public class RecordPaymentCommandHandler(IInvoiceRepository repository)
    : IRequestHandler<RecordPaymentCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(RecordPaymentCommand request, CancellationToken cancellationToken)
    {
        var invoice = await repository.GetByIdAsync(request.InvoiceId, cancellationToken);
        if (invoice is null)
            return new Result<Unit>.NotFound($"Invoice '{request.InvoiceId}' not found.");

        invoice.RecordPayment(request.Amount);

        await repository.SaveChangesAsync(cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}

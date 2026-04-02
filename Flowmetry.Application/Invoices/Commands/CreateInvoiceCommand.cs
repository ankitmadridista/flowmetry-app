using Flowmetry.Application.Common;
using Flowmetry.Domain;
using MediatR;

namespace Flowmetry.Application.Invoices.Commands;

public record CreateInvoiceCommand(Guid CustomerId, decimal Amount, DateOnly DueDate) : IRequest<Result<Guid>>;

public class CreateInvoiceCommandHandler(IInvoiceRepository repository)
    : IRequestHandler<CreateInvoiceCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
    {
        var customerExists = await repository.CustomerExistsAsync(request.CustomerId, cancellationToken);
        if (!customerExists)
            return new Result<Guid>.NotFound($"Customer '{request.CustomerId}' not found.");

        var invoice = Invoice.Create(request.CustomerId, request.Amount, request.DueDate);

        await repository.AddAsync(invoice, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return new Result<Guid>.Success(invoice.Id);
    }
}

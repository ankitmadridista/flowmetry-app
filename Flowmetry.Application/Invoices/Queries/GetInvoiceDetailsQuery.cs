using Flowmetry.Application.Common;
using Flowmetry.Application.Invoices.Dtos;
using MediatR;

namespace Flowmetry.Application.Invoices.Queries;

public record GetInvoiceDetailsQuery(Guid InvoiceId) : IRequest<Result<InvoiceDetailsDto>>;

public class GetInvoiceDetailsQueryHandler(IInvoiceRepository repository)
    : IRequestHandler<GetInvoiceDetailsQuery, Result<InvoiceDetailsDto>>
{
    public async Task<Result<InvoiceDetailsDto>> Handle(GetInvoiceDetailsQuery request, CancellationToken cancellationToken)
    {
        var invoice = await repository.GetByIdWithPaymentsAsync(request.InvoiceId, cancellationToken);
        if (invoice is null)
            return new Result<InvoiceDetailsDto>.NotFound($"Invoice '{request.InvoiceId}' not found.");

        var payments = invoice.Payments
            .Select(p => new PaymentDto(p.Id, p.Amount, p.RecordedAt))
            .ToList();

        var dto = new InvoiceDetailsDto(
            invoice.Id,
            invoice.CustomerId,
            invoice.Amount,
            invoice.DueDate,
            invoice.Status.ToString(),
            payments);

        return new Result<InvoiceDetailsDto>.Success(dto);
    }
}

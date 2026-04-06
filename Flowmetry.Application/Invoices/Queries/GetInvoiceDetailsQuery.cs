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
        var dto = await repository.GetDetailsByIdAsync(request.InvoiceId, cancellationToken);
        if (dto is null)
            return new Result<InvoiceDetailsDto>.NotFound($"Invoice '{request.InvoiceId}' not found.");

        return new Result<InvoiceDetailsDto>.Success(dto);
    }
}

using Flowmetry.Application.Common;
using Flowmetry.Application.Invoices.Dtos;
using MediatR;

namespace Flowmetry.Application.Invoices.Queries;

public record GetInvoicesQuery(InvoiceFilter Filter)
    : IRequest<Result<PagedResult<InvoiceSummaryDto>>>;

public class GetInvoicesQueryHandler(IInvoiceRepository repository)
    : IRequestHandler<GetInvoicesQuery, Result<PagedResult<InvoiceSummaryDto>>>
{
    public async Task<Result<PagedResult<InvoiceSummaryDto>>> Handle(
        GetInvoicesQuery request, CancellationToken cancellationToken)
    {
        var paged = await repository.GetPagedSummariesAsync(request.Filter, cancellationToken);
        return new Result<PagedResult<InvoiceSummaryDto>>.Success(paged);
    }
}

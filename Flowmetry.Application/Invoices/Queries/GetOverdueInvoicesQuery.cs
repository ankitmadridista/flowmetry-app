using Flowmetry.Application.Common;
using Flowmetry.Application.Invoices.Dtos;
using Flowmetry.Domain;
using MediatR;

namespace Flowmetry.Application.Invoices.Queries;

public record GetOverdueInvoicesQuery(bool OverdueOnly = true) : IRequest<Result<IReadOnlyList<InvoiceSummaryDto>>>;

public class GetOverdueInvoicesQueryHandler(IInvoiceRepository repository)
    : IRequestHandler<GetOverdueInvoicesQuery, Result<IReadOnlyList<InvoiceSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<InvoiceSummaryDto>>> Handle(GetOverdueInvoicesQuery request, CancellationToken cancellationToken)
    {
        var invoices = await repository.GetAllAsync(cancellationToken);

        var filtered = request.OverdueOnly
            ? invoices.Where(i => i.Status == InvoiceStatus.Overdue)
            : invoices;

        IReadOnlyList<InvoiceSummaryDto> result = filtered
            .Select(i => new InvoiceSummaryDto(i.Id, i.CustomerId, i.Amount, i.DueDate, i.Status.ToString()))
            .ToList();

        return new Result<IReadOnlyList<InvoiceSummaryDto>>.Success(result);
    }
}

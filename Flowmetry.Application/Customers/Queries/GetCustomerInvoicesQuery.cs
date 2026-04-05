using Flowmetry.Application.Common;
using Flowmetry.Application.Customers;
using Flowmetry.Application.Customers.Dtos;
using MediatR;

namespace Flowmetry.Application.Customers.Queries;

public record GetCustomerInvoicesQuery(Guid CustomerId)
    : IRequest<Result<IReadOnlyList<CustomerInvoiceSummaryDto>>>;

public class GetCustomerInvoicesQueryHandler(ICustomerRepository repository)
    : IRequestHandler<GetCustomerInvoicesQuery, Result<IReadOnlyList<CustomerInvoiceSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<CustomerInvoiceSummaryDto>>> Handle(
        GetCustomerInvoicesQuery request, CancellationToken ct)
    {
        var customer = await repository.GetByIdAsync(request.CustomerId, ct);
        if (customer is null)
            return new Result<IReadOnlyList<CustomerInvoiceSummaryDto>>.NotFound(
                $"Customer '{request.CustomerId}' not found.");

        var invoices = await repository.GetInvoicesByCustomerIdAsync(request.CustomerId, ct);
        var dtos = invoices
            .Select(i => new CustomerInvoiceSummaryDto(i.Id, i.Amount, i.DueDate, i.Status.ToString()))
            .ToList();
        return new Result<IReadOnlyList<CustomerInvoiceSummaryDto>>.Success(dtos);
    }
}

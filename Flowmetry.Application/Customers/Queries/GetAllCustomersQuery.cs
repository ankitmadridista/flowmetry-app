using Flowmetry.Application.Common;
using Flowmetry.Application.Customers;
using Flowmetry.Application.Customers.Dtos;
using MediatR;

namespace Flowmetry.Application.Customers.Queries;

public record GetAllCustomersQuery : IRequest<Result<IReadOnlyList<CustomerSummaryDto>>>;

public class GetAllCustomersQueryHandler(ICustomerRepository repository)
    : IRequestHandler<GetAllCustomersQuery, Result<IReadOnlyList<CustomerSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<CustomerSummaryDto>>> Handle(GetAllCustomersQuery request, CancellationToken ct)
    {
        var customers = await repository.GetAllAsync(ct);
        var dtos = customers.Select(c => new CustomerSummaryDto(
            c.Id, c.Name, c.Email, c.RiskScore, DeriveRiskBand(c.RiskScore)))
            .ToList();
        return new Result<IReadOnlyList<CustomerSummaryDto>>.Success(dtos);
    }

    private static string DeriveRiskBand(int score) => score switch
    {
        <= 30 => "Low",
        <= 65 => "Medium",
        _     => "High"
    };
}

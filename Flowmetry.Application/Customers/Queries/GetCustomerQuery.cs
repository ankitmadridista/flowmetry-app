using Flowmetry.Application.Common;
using Flowmetry.Application.Customers;
using Flowmetry.Application.Customers.Dtos;
using Flowmetry.Domain;
using MediatR;

namespace Flowmetry.Application.Customers.Queries;

public record GetCustomerQuery(Guid CustomerId) : IRequest<Result<CustomerSummaryDto>>;

public class GetCustomerQueryHandler(ICustomerRepository repository)
    : IRequestHandler<GetCustomerQuery, Result<CustomerSummaryDto>>
{
    public async Task<Result<CustomerSummaryDto>> Handle(GetCustomerQuery request, CancellationToken ct)
    {
        var customer = await repository.GetByIdAsync(request.CustomerId, ct);
        if (customer is null)
            return new Result<CustomerSummaryDto>.NotFound($"Customer '{request.CustomerId}' not found.");

        return new Result<CustomerSummaryDto>.Success(ToDto(customer));
    }

    private static CustomerSummaryDto ToDto(Customer c) =>
        new(c.Id, c.Name, c.Email, c.RiskScore, DeriveRiskBand(c.RiskScore));

    private static string DeriveRiskBand(int score) => score switch
    {
        <= 30 => "Low",
        <= 65 => "Medium",
        _     => "High"
    };
}

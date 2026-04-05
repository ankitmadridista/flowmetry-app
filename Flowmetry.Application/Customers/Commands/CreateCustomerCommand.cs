using Flowmetry.Application.Common;
using Flowmetry.Application.Customers;
using Flowmetry.Domain;
using MediatR;

namespace Flowmetry.Application.Customers.Commands;

public record CreateCustomerCommand(string Name, string Email)
    : IRequest<Result<Guid>>;

public class CreateCustomerCommandHandler(ICustomerRepository repository)
    : IRequestHandler<CreateCustomerCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateCustomerCommand request, CancellationToken ct)
    {
        if (await repository.EmailExistsAsync(request.Email, ct))
            return new Result<Guid>.Conflict($"A customer with email '{request.Email}' already exists.");

        var customer = Customer.Create(request.Name, request.Email);
        await repository.AddAsync(customer, ct);
        await repository.SaveChangesAsync(ct);
        return new Result<Guid>.Success(customer.Id);
    }
}

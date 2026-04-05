using Flowmetry.Domain;

namespace Flowmetry.Application.Customers;

public interface ICustomerRepository
{
    Task AddAsync(Customer customer, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Invoice>> GetInvoicesByCustomerIdAsync(Guid customerId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

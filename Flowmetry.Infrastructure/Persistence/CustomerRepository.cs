using Flowmetry.Application.Customers;
using Flowmetry.Domain;
using Microsoft.EntityFrameworkCore;

namespace Flowmetry.Infrastructure.Persistence;

public class CustomerRepository(FlowmetryDbContext context) : ICustomerRepository
{
    public async Task AddAsync(Customer customer, CancellationToken ct = default)
        => await context.Customers.AddAsync(customer, ct);

    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
        => await context.Customers.AnyAsync(c => c.Email == email, ct);

    public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Customers.FindAsync([id], ct);

    public async Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken ct = default)
        => await context.Customers.ToListAsync(ct);

    public async Task<IReadOnlyList<Invoice>> GetInvoicesByCustomerIdAsync(Guid customerId, CancellationToken ct = default)
        => await context.Invoices
            .Where(i => i.CustomerId == customerId)
            .ToListAsync(ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}

using Flowmetry.Application.Invoices;
using Flowmetry.Domain;
using Microsoft.EntityFrameworkCore;

namespace Flowmetry.Infrastructure.Persistence;

public class InvoiceRepository(FlowmetryDbContext context) : IInvoiceRepository
{
    public async Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await context.Invoices.FindAsync([id], cancellationToken);

    public async Task<Invoice?> GetByIdWithPaymentsAsync(Guid id, CancellationToken cancellationToken = default)
        => await context.Invoices
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Invoice>> GetAllAsync(CancellationToken cancellationToken = default)
        => await context.Invoices.ToListAsync(cancellationToken);

    public async Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default)
        => await context.Invoices.AddAsync(invoice, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await context.SaveChangesAsync(cancellationToken);

    public async Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken cancellationToken = default)
        => await context.Customers.AnyAsync(c => c.Id == customerId, cancellationToken);
}

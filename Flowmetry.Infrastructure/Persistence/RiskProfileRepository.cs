using Flowmetry.Application.RiskScoring;
using Flowmetry.Domain;
using Microsoft.EntityFrameworkCore;

namespace Flowmetry.Infrastructure.Persistence;

public class RiskProfileRepository(FlowmetryDbContext dbContext)
    : IRiskProfileRepository
{
    public async Task<IReadOnlyList<Invoice>> GetInvoicesWithPaymentsByCustomerAsync(
        Guid customerId,
        CancellationToken ct = default)
        => await dbContext.Invoices
            .Where(i => i.CustomerId == customerId
                     && i.Status != InvoiceStatus.Cancelled)
            .Include(i => i.Payments)
            .ToListAsync(ct);

    public async Task<bool> CustomerExistsAsync(
        Guid customerId,
        CancellationToken ct = default)
        => await dbContext.Customers
            .AnyAsync(c => c.Id == customerId, ct);
}

using Flowmetry.Application.Common;
using Flowmetry.Application.Invoices.Dtos;
using Flowmetry.Domain;

namespace Flowmetry.Application.Invoices;

public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Invoice?> GetByIdWithPaymentsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<PagedResult<Invoice>> GetPagedAsync(InvoiceFilter filter, CancellationToken ct = default);
    Task<PagedResult<InvoiceSummaryDto>> GetPagedSummariesAsync(InvoiceFilter filter, CancellationToken ct = default);
    Task<InvoiceDetailsDto?> GetDetailsByIdAsync(Guid id, CancellationToken ct = default);
}

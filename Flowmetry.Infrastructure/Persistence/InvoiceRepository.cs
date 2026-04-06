using Flowmetry.Application.Common;
using Flowmetry.Application.Invoices;
using Flowmetry.Application.Invoices.Dtos;
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

    public async Task<PagedResult<Invoice>> GetPagedAsync(
        InvoiceFilter filter, CancellationToken ct = default)
    {
        var query = BuildInvoiceQuery(filter);
        var totalCount = await query.CountAsync(ct);
        query = ApplySort(query, filter);

        var items = await query
            .Skip(filter.Page * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return new PagedResult<Invoice>(items, totalCount, filter.Page, filter.PageSize);
    }

    public async Task<PagedResult<InvoiceSummaryDto>> GetPagedSummariesAsync(
        InvoiceFilter filter, CancellationToken ct = default)
    {
        var query = BuildInvoiceQuery(filter);
        var totalCount = await query.CountAsync(ct);
        query = ApplySort(query, filter);

        var items = await query
            .Skip(filter.Page * filter.PageSize)
            .Take(filter.PageSize)
            .Join(context.Customers,
                  i => i.CustomerId,
                  c => c.Id,
                  (i, c) => new InvoiceSummaryDto(
                      i.Id,
                      i.InvoiceNumber,
                      c.Name,
                      i.Amount,
                      i.DueDate,
                      i.Status.ToString()))
            .ToListAsync(ct);

        return new PagedResult<InvoiceSummaryDto>(items, totalCount, filter.Page, filter.PageSize);
    }

    public async Task<InvoiceDetailsDto?> GetDetailsByIdAsync(Guid id, CancellationToken ct = default)
    {
        var result = await context.Invoices
            .Include(i => i.Payments)
            .Where(i => i.Id == id)
            .Join(context.Customers,
                  i => i.CustomerId,
                  c => c.Id,
                  (i, c) => new { Invoice = i, CustomerName = c.Name })
            .FirstOrDefaultAsync(ct);

        if (result is null) return null;

        var payments = result.Invoice.Payments
            .Select(p => new PaymentDto(p.Id, p.Amount, p.RecordedAt))
            .ToList();

        return new InvoiceDetailsDto(
            result.Invoice.Id,
            result.Invoice.InvoiceNumber,
            result.Invoice.CustomerId,
            result.CustomerName,
            result.Invoice.Amount,
            result.Invoice.DueDate,
            result.Invoice.Status.ToString(),
            payments);
    }

    private IQueryable<Invoice> BuildInvoiceQuery(InvoiceFilter filter)
    {
        var query = context.Invoices.AsQueryable();

        if (filter.CustomerId.HasValue)
            query = query.Where(i => i.CustomerId == filter.CustomerId.Value);
        if (filter.Status.HasValue)
            query = query.Where(i => i.Status == filter.Status.Value);
        if (filter.DueDateFrom.HasValue)
            query = query.Where(i => i.DueDate >= filter.DueDateFrom.Value);
        if (filter.DueDateTo.HasValue)
            query = query.Where(i => i.DueDate <= filter.DueDateTo.Value);
        if (!string.IsNullOrWhiteSpace(filter.CustomerName))
        {
            var name = filter.CustomerName.ToLower();
            var matchingCustomerIds = context.Customers
                .Where(c => c.Name.ToLower().Contains(name))
                .Select(c => c.Id);
            query = query.Where(i => matchingCustomerIds.Contains(i.CustomerId));
        }

        return query;
    }

    private static IQueryable<Invoice> ApplySort(IQueryable<Invoice> query, InvoiceFilter filter) =>
        (filter.SortBy, filter.SortDir) switch
        {
            (SortField.DueDate, SortDirection.Asc)  => query.OrderBy(i => i.DueDate),
            (SortField.DueDate, SortDirection.Desc) => query.OrderByDescending(i => i.DueDate),
            (SortField.Amount,  SortDirection.Asc)  => query.OrderBy(i => i.Amount),
            (SortField.Amount,  SortDirection.Desc) => query.OrderByDescending(i => i.Amount),
            (SortField.Status,  SortDirection.Asc)  => query.OrderBy(i => i.Status),
            (SortField.Status,  SortDirection.Desc) => query.OrderByDescending(i => i.Status),
            _                                        => query.OrderBy(i => i.DueDate)
        };
}

using CsCheck;
using Flowmetry.Application.Common;
using Flowmetry.Application.Invoices;
using Flowmetry.Application.Invoices.Dtos;
using Flowmetry.Application.Invoices.Queries;
using Flowmetry.Domain;

namespace Flowmetry.API.Tests.PropertyTests;

/// <summary>
/// Property-based tests for invoice query handlers (Tasks 12.7 and 12.8).
/// </summary>
public class InvoiceQueryPropertyTests
{
    // ── Property 10 (Task 12.7) ───────────────────────────────────────────────

    [Fact]
    public async Task Property10_OverdueQuery_ReturnsOnlyOverdueInvoices()
    {
        // Feature: invoice-domain, Property 10: overdue query filter returns correct subset
        await Gen.Int[1, 10]
            .SampleAsync(async invoiceCount =>
            {
                var repo = new InMemoryInvoiceRepository();
                var customerId = Guid.NewGuid();
                var dueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30));

                // Create invoices in various states
                for (int i = 0; i < invoiceCount; i++)
                {
                    var invoice = Invoice.Create(customerId, 100m * (i + 1), dueDate);

                    // Alternate between Draft, Sent, and Overdue
                    var state = i % 3;
                    if (state >= 1) invoice.MarkAsSent();
                    if (state == 2) invoice.MarkOverdue();

                    await repo.AddAsync(invoice);
                }
                await repo.SaveChangesAsync();

                var handler = new GetOverdueInvoicesQueryHandler(repo);

                // OverdueOnly = true → only Overdue invoices
                var result = await handler.Handle(new GetOverdueInvoicesQuery(OverdueOnly: true), CancellationToken.None);
                var success = Assert.IsType<Result<IReadOnlyList<InvoiceSummaryDto>>.Success>(result);

                Assert.All(success.Value, dto =>
                    Assert.Equal("Overdue", dto.Status));

                // OverdueOnly = false → all invoices
                var allResult = await handler.Handle(new GetOverdueInvoicesQuery(OverdueOnly: false), CancellationToken.None);
                var allSuccess = Assert.IsType<Result<IReadOnlyList<InvoiceSummaryDto>>.Success>(allResult);

                Assert.Equal(invoiceCount, allSuccess.Value.Count);
            });
    }

    // ── Property 11 (Task 12.8) ───────────────────────────────────────────────

    [Fact]
    public async Task Property11_GetInvoiceDetails_ReturnsAllNPayments()
    {
        // Feature: invoice-domain, Property 11: GetInvoiceDetails returns all N payment records
        await Gen.Int[1, 5]
            .SampleAsync(async paymentCount =>
            {
                var repo = new InMemoryInvoiceRepository();
                var customerId = Guid.NewGuid();
                var invoiceAmount = 100m * paymentCount; // ensure enough balance for N payments

                var invoice = Invoice.Create(customerId, invoiceAmount, DateOnly.FromDateTime(DateTime.Today.AddDays(30)));
                invoice.MarkAsSent();

                // Record exactly N payments of equal size
                var paymentAmount = invoiceAmount / paymentCount;
                for (int i = 0; i < paymentCount; i++)
                {
                    invoice.RecordPayment(paymentAmount);
                }

                await repo.AddAsync(invoice);
                await repo.SaveChangesAsync();

                var handler = new GetInvoiceDetailsQueryHandler(repo);
                var result = await handler.Handle(new GetInvoiceDetailsQuery(invoice.Id), CancellationToken.None);

                var success = Assert.IsType<Result<InvoiceDetailsDto>.Success>(result);
                Assert.Equal(paymentCount, success.Value.Payments.Count);
            });
    }
}

// ── In-memory repository for property tests ───────────────────────────────────

internal class InMemoryInvoiceRepository : IInvoiceRepository
{
    private readonly List<Invoice> _invoices = new();

    public Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_invoices.FirstOrDefault(i => i.Id == id));

    public Task<Invoice?> GetByIdWithPaymentsAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_invoices.FirstOrDefault(i => i.Id == id));

    public Task<IReadOnlyList<Invoice>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Invoice>>(_invoices.AsReadOnly());

    public Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        _invoices.Add(invoice);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}

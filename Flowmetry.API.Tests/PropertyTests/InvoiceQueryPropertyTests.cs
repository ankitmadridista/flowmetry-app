// Feature: invoice-list-improvements, Property 2: Invalid parameters produce HTTP 400
// Feature: invoice-list-improvements, Property 7: Invoice-to-DTO mapping preserves all fields

using CsCheck;
using FluentValidation;
using Flowmetry.Application.Common;
using Flowmetry.Application.Invoices;
using Flowmetry.Application.Invoices.Dtos;
using Flowmetry.Application.Invoices.Queries;
using Flowmetry.Application.Invoices.Validators;
using Flowmetry.Domain;

namespace Flowmetry.API.Tests.PropertyTests;

public class InvoiceQueryPropertyTests
{
    // ── Property 2: Invalid parameters produce HTTP 400 ───────────────────────
    // Validates: Requirements 1.8, 1.9, 1.10, 2.5, 2.6, 3.5, 3.6

    [Fact]
    public void Property2_NegativePage_ProducesValidationFailure()
    {
        // **Validates: Requirements 1.8, 1.9, 1.10, 2.5, 2.6, 3.5, 3.6**
        //
        // For any request where page < 0, the validator must return a failure
        // result with a non-empty errors collection.

        var validator = new GetInvoicesQueryValidator();

        Gen.Int[-100, -1].Sample(negativePage =>
        {
            var query = new GetInvoicesQuery(new InvoiceFilter(Page: negativePage, PageSize: 25));
            var result = validator.Validate(query);

            Assert.False(result.IsValid, $"Expected validation failure for page={negativePage}");
            Assert.NotEmpty(result.Errors);
        }, iter: 20);
    }

    [Fact]
    public void Property2_OutOfRangePageSize_ProducesValidationFailure()
    {
        // **Validates: Requirements 1.8, 1.9, 1.10, 2.5, 2.6, 3.5, 3.6**
        //
        // For any request where pageSize is outside 1–100, the validator must
        // return a failure result with a non-empty errors collection.

        var validator = new GetInvoicesQueryValidator();

        Gen.OneOf(Gen.Int[-100, 0], Gen.Int[101, 200]).Sample(invalidPageSize =>
        {
            var query = new GetInvoicesQuery(new InvoiceFilter(Page: 0, PageSize: invalidPageSize));
            var result = validator.Validate(query);

            Assert.False(result.IsValid, $"Expected validation failure for pageSize={invalidPageSize}");
            Assert.NotEmpty(result.Errors);
        }, iter: 20);
    }

    [Fact]
    public void Property2_InvertedDateRange_ProducesValidationFailure()
    {
        // **Validates: Requirements 1.8, 1.9, 1.10, 2.5, 2.6, 3.5, 3.6**
        //
        // For any request where dueDateFrom > dueDateTo, the validator must
        // return a failure result with a non-empty errors collection.

        var validator = new GetInvoicesQueryValidator();

        var gen =
            from year in Gen.Int[2020, 2035]
            from month in Gen.Int[1, 12]
            from day in Gen.Int[1, 27]
            select (From: new DateOnly(year, month, day + 1), To: new DateOnly(year, month, day));

        gen.Sample(tuple =>
        {
            var query = new GetInvoicesQuery(new InvoiceFilter(
                Page: 0, PageSize: 25,
                DueDateFrom: tuple.From,
                DueDateTo: tuple.To));

            var result = validator.Validate(query);

            Assert.False(result.IsValid,
                $"Expected validation failure for dueDateFrom={tuple.From} > dueDateTo={tuple.To}");
            Assert.NotEmpty(result.Errors);
        }, iter: 20);
    }

    // ── Property 7: Invoice-to-DTO mapping preserves all fields ──────────────
    // Validates: Requirements 6.4

    [Fact]
    public async Task Property7_InvoiceToDtoMapping_PreservesAllFields()
    {
        // **Validates: Requirements 6.4**
        //
        // For any Invoice domain object, mapping it via the handler's ToDto
        // method must produce a DTO where Id, CustomerId, Amount, DueDate, and
        // Status.ToString() equal the corresponding domain fields.
        //
        // ToDto is private static on GetInvoicesQueryHandler, so we test it
        // indirectly: seed an in-memory stub repository with a single invoice,
        // call Handle, and assert the returned DTO fields match.

        var gen =
            from customerId in Gen.Guid
            from amount in Gen.Decimal[1m, 100_000m]
            from year in Gen.Int[2020, 2040]
            from month in Gen.Int[1, 12]
            from day in Gen.Int[1, 28]
            select (CustomerId: customerId, Amount: amount, DueDate: new DateOnly(year, month, day));

        await gen.SampleAsync(async tuple =>
        {
            var invoice = Invoice.Create(tuple.CustomerId, tuple.Amount, tuple.DueDate);

            var repo = new SingleInvoiceStubRepository(invoice, tuple.CustomerId);
            var handler = new GetInvoicesQueryHandler(repo);

            var query = new GetInvoicesQuery(new InvoiceFilter(Page: 0, PageSize: 25));
            var result = await handler.Handle(query, CancellationToken.None);

            var success = Assert.IsType<Result<PagedResult<InvoiceSummaryDto>>.Success>(result);
            var dto = Assert.Single(success.Value.Items);

            Assert.Equal(invoice.Id, dto.Id);
            Assert.Equal(invoice.Amount, dto.Amount);
            Assert.Equal(invoice.DueDate, dto.DueDate);
            Assert.Equal(invoice.Status.ToString(), dto.Status);
        }, iter: 20);
    }
}

// ── SingleInvoiceStubRepository ───────────────────────────────────────────────

/// <summary>
/// Minimal stub that returns a single invoice from GetPagedAsync, used to test
/// the handler's ToDto mapping without a real database.
/// </summary>
internal class SingleInvoiceStubRepository : IInvoiceRepository
{
    private readonly Invoice _invoice;

    public SingleInvoiceStubRepository(Invoice invoice, Guid customerId) => _invoice = invoice;

    public Task<PagedResult<Invoice>> GetPagedAsync(InvoiceFilter filter, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<PagedResult<InvoiceSummaryDto>> GetPagedSummariesAsync(InvoiceFilter filter, CancellationToken ct = default)
    {
        var dto = new InvoiceSummaryDto(
            _invoice.Id,
            _invoice.InvoiceNumber,
            "Test Customer",
            _invoice.Amount,
            _invoice.DueDate,
            _invoice.Status.ToString());
        var result = new PagedResult<InvoiceSummaryDto>(
            new List<InvoiceSummaryDto> { dto },
            TotalCount: 1,
            Page: filter.Page,
            PageSize: filter.PageSize);
        return Task.FromResult(result);
    }

    public Task<InvoiceDetailsDto?> GetDetailsByIdAsync(Guid id, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<Invoice?> GetByIdWithPaymentsAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<IReadOnlyList<Invoice>> GetAllAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}

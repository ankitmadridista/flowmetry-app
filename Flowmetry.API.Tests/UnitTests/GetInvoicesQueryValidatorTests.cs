// Feature: invoice-list-improvements
// Unit tests for GetInvoicesQueryValidator
// Validates: Requirements 1.10, 2.5, 2.6

using Flowmetry.Application.Invoices;
using Flowmetry.Application.Invoices.Queries;
using Flowmetry.Application.Invoices.Validators;

namespace Flowmetry.API.Tests.UnitTests;

public class GetInvoicesQueryValidatorTests
{
    private readonly GetInvoicesQueryValidator _validator = new();

    // ── Invalid inputs ────────────────────────────────────────────────────────

    [Fact]
    public void Page_Negative_FailsValidation()
    {
        var query = new GetInvoicesQuery(new InvoiceFilter(Page: -1));
        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("page", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PageSize_Zero_FailsValidation()
    {
        var query = new GetInvoicesQuery(new InvoiceFilter(PageSize: 0));
        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("pageSize", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PageSize_101_FailsValidation()
    {
        var query = new GetInvoicesQuery(new InvoiceFilter(PageSize: 101));
        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("pageSize", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DueDateFrom_GreaterThan_DueDateTo_FailsValidation()
    {
        var query = new GetInvoicesQuery(new InvoiceFilter(
            DueDateFrom: new DateOnly(2025, 6, 1),
            DueDateTo:   new DateOnly(2025, 5, 1)));
        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("dueDateFrom", StringComparison.OrdinalIgnoreCase));
    }

    // ── Valid boundary values ─────────────────────────────────────────────────

    [Fact]
    public void Page_Zero_PassesValidation()
    {
        var query = new GetInvoicesQuery(new InvoiceFilter(Page: 0));
        var result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void PageSize_One_PassesValidation()
    {
        var query = new GetInvoicesQuery(new InvoiceFilter(PageSize: 1));
        var result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void PageSize_100_PassesValidation()
    {
        var query = new GetInvoicesQuery(new InvoiceFilter(PageSize: 100));
        var result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidFilter_NoDateRange_PassesValidation()
    {
        var query = new GetInvoicesQuery(new InvoiceFilter());
        var result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }
}

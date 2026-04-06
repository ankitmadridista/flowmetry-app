// Feature: invoice-list-improvements, Property 1: Filter predicates are exclusive
// Feature: invoice-list-improvements, Property 3: Page size is respected
// Feature: invoice-list-improvements, Property 4: TotalCount is page-independent
// Feature: invoice-list-improvements, Property 5: Response envelope is always present
// Feature: invoice-list-improvements, Property 6: Sort ordering is consistent

using CsCheck;
using Flowmetry.Application.Common;
using Flowmetry.Application.Invoices;
using Flowmetry.Domain;
using Flowmetry.Infrastructure;
using Flowmetry.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Flowmetry.API.Tests.PropertyTests;

public class InvoiceRepositoryPropertyTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static FlowmetryDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<FlowmetryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new FlowmetryDbContext(options);
    }

    private static async Task SeedAsync(FlowmetryDbContext ctx, IEnumerable<Invoice> invoices)
    {
        ctx.Invoices.AddRange(invoices);
        await ctx.SaveChangesAsync();
    }

    private static readonly Gen<InvoiceStatus> GenStatus =
        Gen.OneOf(
            Gen.Const(InvoiceStatus.Draft),
            Gen.Const(InvoiceStatus.Sent),
            Gen.Const(InvoiceStatus.PartiallyPaid),
            Gen.Const(InvoiceStatus.Paid),
            Gen.Const(InvoiceStatus.Overdue),
            Gen.Const(InvoiceStatus.Cancelled));

    private static readonly Gen<DateOnly> GenDate =
        from year in Gen.Int[2020, 2030]
        from month in Gen.Int[1, 12]
        from day in Gen.Int[1, 28]
        select new DateOnly(year, month, day);

    private static Invoice CreateInvoiceWithStatus(Guid customerId, decimal amount, DateOnly dueDate, InvoiceStatus status)
    {
        var invoice = Invoice.Create(customerId, amount, dueDate);
        // Override Status via reflection since Invoice.Create always sets Draft
        typeof(Invoice).GetProperty(nameof(Invoice.Status))!.SetValue(invoice, status);
        return invoice;
    }

    private static readonly Gen<Invoice> GenInvoice =
        from customerId in Gen.Guid
        from amount in Gen.Decimal[1m, 10_000m]
        from dueDate in GenDate
        from status in GenStatus
        select CreateInvoiceWithStatus(customerId, amount, dueDate, status);

    // ── Property 1: Filter predicates are exclusive ───────────────────────────

    [Fact]
    public async Task Property1_FilterPredicatesAreExclusive()
    {
        // **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 1.5**
        //
        // For any InvoiceFilter with one or more active predicates, every invoice
        // in the returned items must satisfy all active predicates simultaneously.

        await Gen.Int[1, 20].SampleAsync(async count =>
        {
            // Generate a list of invoices
            var invoices = new List<Invoice>();
            for (int i = 0; i < count; i++)
            {
                var inv = CreateInvoiceWithStatus(
                    Guid.NewGuid(), 100m * (i + 1),
                    new DateOnly(2025, 1 + (i % 12 == 0 ? 11 : i % 12), 1 + (i % 28)),
                    (InvoiceStatus)(i % 6));
                invoices.Add(inv);
            }

            // Pick a status from the seeded invoices to guarantee at least one match
            var targetStatus = invoices[0].Status;
            var filter = new InvoiceFilter(Status: targetStatus, Page: 0, PageSize: 100);

            await using var ctx = CreateContext();
            await SeedAsync(ctx, invoices);

            var repo = new InvoiceRepository(ctx);
            var result = await repo.GetPagedAsync(filter);

            foreach (var item in result.Items)
                Assert.Equal(targetStatus, item.Status);
        }, iter: 20);
    }

    [Fact]
    public async Task Property1b_CustomerIdFilterIsExclusive()
    {
        // **Validates: Requirements 1.1**
        //
        // When filtering by customerId, only invoices for that customer are returned.

        await Gen.Int[1, 10].SampleAsync(async count =>
        {
            var targetCustomerId = Guid.NewGuid();
            var invoices = new List<Invoice>();

            // Add invoices for the target customer
            for (int i = 0; i < count; i++)
                invoices.Add(Invoice.Create(targetCustomerId, 100m * (i + 1), new DateOnly(2025, 6, 15)));

            // Add invoices for other customers
            for (int i = 0; i < count; i++)
                invoices.Add(Invoice.Create(Guid.NewGuid(), 200m * (i + 1), new DateOnly(2025, 6, 15)));

            var filter = new InvoiceFilter(CustomerId: targetCustomerId, Page: 0, PageSize: 100);

            await using var ctx = CreateContext();
            await SeedAsync(ctx, invoices);

            var repo = new InvoiceRepository(ctx);
            var result = await repo.GetPagedAsync(filter);

            foreach (var item in result.Items)
                Assert.Equal(targetCustomerId, item.CustomerId);
        }, iter: 20);
    }

    [Fact]
    public async Task Property1c_DateRangeFilterIsExclusive()
    {
        // **Validates: Requirements 1.3, 1.4, 1.5**
        //
        // When filtering by dueDateFrom/dueDateTo, only invoices within the range are returned.

        await Gen.Int[1, 10].SampleAsync(async count =>
        {
            var from = new DateOnly(2025, 3, 1);
            var to   = new DateOnly(2025, 6, 30);

            var invoices = new List<Invoice>();
            // Invoices inside range
            for (int i = 0; i < count; i++)
                invoices.Add(Invoice.Create(Guid.NewGuid(), 100m, new DateOnly(2025, 3 + (i % 4), 15)));
            // Invoices outside range
            for (int i = 0; i < count; i++)
                invoices.Add(Invoice.Create(Guid.NewGuid(), 100m, new DateOnly(2024, 1 + (i % 12), 1)));

            var filter = new InvoiceFilter(DueDateFrom: from, DueDateTo: to, Page: 0, PageSize: 100);

            await using var ctx = CreateContext();
            await SeedAsync(ctx, invoices);

            var repo = new InvoiceRepository(ctx);
            var result = await repo.GetPagedAsync(filter);

            foreach (var item in result.Items)
            {
                Assert.True(item.DueDate >= from, $"DueDate {item.DueDate} < DueDateFrom {from}");
                Assert.True(item.DueDate <= to,   $"DueDate {item.DueDate} > DueDateTo {to}");
            }
        }, iter: 20);
    }

    // ── Property 3: Page size is respected ───────────────────────────────────

    [Fact]
    public async Task Property3_PageSizeIsRespected()
    {
        // **Validates: Requirements 2.2**
        //
        // For any valid InvoiceFilter with pageSize in 1–100, the length of
        // result.Items must be <= filter.PageSize.

        await Gen.Int[1, 100].SampleAsync(async pageSize =>
        {
            var invoices = Enumerable.Range(0, 50)
                .Select(i => Invoice.Create(Guid.NewGuid(), 100m * (i + 1), new DateOnly(2025, 6, 15)))
                .ToList();

            var filter = new InvoiceFilter(Page: 0, PageSize: pageSize);

            await using var ctx = CreateContext();
            await SeedAsync(ctx, invoices);

            var repo = new InvoiceRepository(ctx);
            var result = await repo.GetPagedAsync(filter);

            Assert.True(result.Items.Count <= filter.PageSize,
                $"Items.Count {result.Items.Count} > PageSize {filter.PageSize}");
        }, iter: 20);
    }

    // ── Property 4: TotalCount is page-independent ───────────────────────────

    [Fact]
    public async Task Property4_TotalCountIsPageIndependent()
    {
        // **Validates: Requirements 2.7, 4.3**
        //
        // For any fixed set of active filters, totalCount must be identical
        // regardless of which page is requested.

        await Gen.Int[1, 100].SampleAsync(async pageSize =>
        {
            var invoices = Enumerable.Range(0, 30)
                .Select(i => Invoice.Create(Guid.NewGuid(), 100m * (i + 1), new DateOnly(2025, 6, 15)))
                .ToList();

            await using var ctx = CreateContext();
            await SeedAsync(ctx, invoices);

            var repo = new InvoiceRepository(ctx);

            var result0 = await repo.GetPagedAsync(new InvoiceFilter(Page: 0, PageSize: pageSize));
            var result1 = await repo.GetPagedAsync(new InvoiceFilter(Page: 1, PageSize: pageSize));

            Assert.Equal(result0.TotalCount, result1.TotalCount);
        }, iter: 20);
    }

    // ── Property 5: Response envelope is always present ───────────────────────────

    [Fact]
    public async Task Property5_ResponseEnvelopeIsAlwaysPresent()
    {
        // Feature: invoice-list-improvements, Property 5: Response envelope is always present
        // **Validates: Requirements 4.1, 4.4, 4.5**
        //
        // For any valid InvoiceFilter, the returned PagedResult must have:
        // - Page == filter.Page
        // - PageSize == filter.PageSize
        // - TotalCount >= 0
        // - Items is not null

        var gen =
            from page in Gen.Int[0, 10]
            from pageSize in Gen.Int[1, 100]
            from count in Gen.Int[0, 30]
            select (Page: page, PageSize: pageSize, Count: count);

        await gen.SampleAsync(async tuple =>
        {
            var invoices = Enumerable.Range(0, tuple.Count)
                .Select(i => Invoice.Create(Guid.NewGuid(), 100m * (i + 1), new DateOnly(2025, 6, 15)))
                .ToList();

            var filter = new InvoiceFilter(Page: tuple.Page, PageSize: tuple.PageSize);

            await using var ctx = CreateContext();
            await SeedAsync(ctx, invoices);

            var repo = new InvoiceRepository(ctx);
            var result = await repo.GetPagedAsync(filter);

            Assert.Equal(filter.Page, result.Page);
            Assert.Equal(filter.PageSize, result.PageSize);
            Assert.True(result.TotalCount >= 0, $"TotalCount {result.TotalCount} < 0");
            Assert.NotNull(result.Items);
        }, iter: 20);
    }

    // ── Property 6: Sort ordering is consistent ───────────────────────────────

    [Fact]
    public async Task Property6_SortByDueDateAscIsOrdered()
    {
        // **Validates: Requirements 3.1, 3.2**
        //
        // When sorting by DueDate Asc, adjacent items must be in non-decreasing order.

        await Gen.Int[1, 20].SampleAsync(async count =>
        {
            var invoices = Enumerable.Range(0, count)
                .Select(i => Invoice.Create(Guid.NewGuid(), 100m, new DateOnly(2025, 1 + (i % 12 == 0 ? 11 : i % 12), 1 + (i % 28))))
                .ToList();

            await using var ctx = CreateContext();
            await SeedAsync(ctx, invoices);

            var repo = new InvoiceRepository(ctx);
            var result = await repo.GetPagedAsync(new InvoiceFilter(Page: 0, PageSize: 100, SortBy: SortField.DueDate, SortDir: SortDirection.Asc));

            for (int i = 0; i < result.Items.Count - 1; i++)
                Assert.True(result.Items[i].DueDate <= result.Items[i + 1].DueDate,
                    $"[{i}] DueDate Asc: {result.Items[i].DueDate} > {result.Items[i + 1].DueDate}");
        }, iter: 20);
    }

    [Fact]
    public async Task Property6_SortByDueDateDescIsOrdered()
    {
        // **Validates: Requirements 3.1, 3.2**

        await Gen.Int[1, 20].SampleAsync(async count =>
        {
            var invoices = Enumerable.Range(0, count)
                .Select(i => Invoice.Create(Guid.NewGuid(), 100m, new DateOnly(2025, 1 + (i % 12 == 0 ? 11 : i % 12), 1 + (i % 28))))
                .ToList();

            await using var ctx = CreateContext();
            await SeedAsync(ctx, invoices);

            var repo = new InvoiceRepository(ctx);
            var result = await repo.GetPagedAsync(new InvoiceFilter(Page: 0, PageSize: 100, SortBy: SortField.DueDate, SortDir: SortDirection.Desc));

            for (int i = 0; i < result.Items.Count - 1; i++)
                Assert.True(result.Items[i].DueDate >= result.Items[i + 1].DueDate,
                    $"[{i}] DueDate Desc: {result.Items[i].DueDate} < {result.Items[i + 1].DueDate}");
        }, iter: 20);
    }

    [Fact]
    public async Task Property6_SortByAmountAscIsOrdered()
    {
        // **Validates: Requirements 3.1, 3.2**

        await Gen.Int[1, 20].SampleAsync(async count =>
        {
            var invoices = Enumerable.Range(0, count)
                .Select(i => Invoice.Create(Guid.NewGuid(), 100m * (i + 1), new DateOnly(2025, 6, 15)))
                .ToList();

            await using var ctx = CreateContext();
            await SeedAsync(ctx, invoices);

            var repo = new InvoiceRepository(ctx);
            var result = await repo.GetPagedAsync(new InvoiceFilter(Page: 0, PageSize: 100, SortBy: SortField.Amount, SortDir: SortDirection.Asc));

            for (int i = 0; i < result.Items.Count - 1; i++)
                Assert.True(result.Items[i].Amount <= result.Items[i + 1].Amount,
                    $"[{i}] Amount Asc: {result.Items[i].Amount} > {result.Items[i + 1].Amount}");
        }, iter: 20);
    }

    [Fact]
    public async Task Property6_SortByAmountDescIsOrdered()
    {
        // **Validates: Requirements 3.1, 3.2**

        await Gen.Int[1, 20].SampleAsync(async count =>
        {
            var invoices = Enumerable.Range(0, count)
                .Select(i => Invoice.Create(Guid.NewGuid(), 100m * (i + 1), new DateOnly(2025, 6, 15)))
                .ToList();

            await using var ctx = CreateContext();
            await SeedAsync(ctx, invoices);

            var repo = new InvoiceRepository(ctx);
            var result = await repo.GetPagedAsync(new InvoiceFilter(Page: 0, PageSize: 100, SortBy: SortField.Amount, SortDir: SortDirection.Desc));

            for (int i = 0; i < result.Items.Count - 1; i++)
                Assert.True(result.Items[i].Amount >= result.Items[i + 1].Amount,
                    $"[{i}] Amount Desc: {result.Items[i].Amount} < {result.Items[i + 1].Amount}");
        }, iter: 20);
    }
}

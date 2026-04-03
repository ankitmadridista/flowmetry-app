// Feature: cashflow-dashboard, Property 1: InitialiseAsync inserts row with paid_amount=0 and status=Draft for any invoice ID and amount
// Feature: cashflow-dashboard, Property 2: ApplyPaymentAsync accumulates paid_amount correctly across any sequence of positive payments
// Feature: cashflow-dashboard, Property 3: MarkSettledAsync sets status=Paid and non-null settled_at for any non-Paid projection
// Feature: cashflow-dashboard, Property 4: MarkSettledAsync is idempotent — returns Success without row changes when status is already Paid
// Feature: cashflow-dashboard, Property 5: ApplyPaymentAsync and MarkOverdueAsync return ServiceResult.Failure for any unknown invoice ID
// Feature: cashflow-dashboard, Property 6: ApplyPaymentAsync returns ServiceResult.Failure for any paymentAmount <= 0
// Feature: cashflow-dashboard, Property 7: second call to InitialiseAsync for the same invoice ID returns ServiceResult.Failure

using CsCheck;
using Flowmetry.Application.Common;
using Flowmetry.Infrastructure;
using Flowmetry.Infrastructure.Projections;
using Microsoft.EntityFrameworkCore;

namespace Flowmetry.API.Tests.PropertyTests;

/// <summary>
/// Property-based tests for EfCashflowProjectionService.
/// Properties 1–7 exercise the real EF Core implementation against an in-memory database.
/// </summary>
public class CashflowProjectionServicePropertyTests
{
    private static FlowmetryDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<FlowmetryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new FlowmetryDbContext(options);
    }

    // ── Property 1: InitialiseAsync inserts correct initial state ─────────────
    // Validates: Requirements 2.2, 1.6

    [Fact]
    public async Task Property1_InitialiseAsync_InsertsRowWithZeroPaidAmountAndDraftStatus()
    {
        // **Validates: Requirements 2.2, 1.6**
        //
        // For any invoice ID (Guid) and invoice amount (positive decimal), calling
        // InitialiseAsync should return ServiceResult.Success and insert a row with
        // PaidAmount == 0 and Status == "Draft".

        var gen =
            from invoiceId in Gen.Guid
            from amount in Gen.Decimal[0.01m, 1_000_000m]
            select (InvoiceId: invoiceId, Amount: amount);

        await gen.SampleAsync(async tuple =>
        {
            var (invoiceId, amount) = tuple;

            await using var db = CreateDb();
            var svc = new EfCashflowProjectionService(db);

            // Act
            var result = await svc.InitialiseAsync(invoiceId, amount, CancellationToken.None);

            // Assert: returns Success
            Assert.IsType<ServiceResult.Success>(result);

            // Assert: row inserted with correct initial state
            var row = await db.CashflowProjections.FindAsync(invoiceId);
            Assert.NotNull(row);
            Assert.Equal(0m, row.PaidAmount);
            Assert.Equal("Draft", row.Status);
            Assert.Equal(amount, row.InvoiceAmount);
            Assert.Null(row.SettledAt);
        });
    }

    // ── Property 2: ApplyPaymentAsync accumulates paid_amount correctly ───────
    // Validates: Requirements 5.2, 1.2

    [Fact]
    public async Task Property2_ApplyPaymentAsync_AccumulatesPaidAmountAcrossSequenceOfPayments()
    {
        // **Validates: Requirements 5.2, 1.2**
        //
        // For any invoice ID and any sequence of 1–5 positive payment amounts,
        // calling ApplyPaymentAsync for each should return ServiceResult.Success
        // for each call, and result in PaidAmount equal to the sum of all payments.

        var gen =
            from invoiceId in Gen.Guid
            from invoiceAmount in Gen.Decimal[100m, 1_000_000m]
            from payments in Gen.Decimal[0.01m, 1000m].List[1, 5]
            select (InvoiceId: invoiceId, InvoiceAmount: invoiceAmount, Payments: payments);

        await gen.SampleAsync(async tuple =>
        {
            var (invoiceId, invoiceAmount, payments) = tuple;

            await using var db = CreateDb();
            var svc = new EfCashflowProjectionService(db);

            // Seed the projection row
            await svc.InitialiseAsync(invoiceId, invoiceAmount, CancellationToken.None);

            // Act: apply each payment
            decimal runningTotal = 0m;
            foreach (var payment in payments)
            {
                runningTotal += payment;
                var result = await svc.ApplyPaymentAsync(invoiceId, payment, runningTotal, CancellationToken.None);
                Assert.IsType<ServiceResult.Success>(result);
            }

            // Assert: PaidAmount equals sum of all payments
            var row = await db.CashflowProjections.FindAsync(invoiceId);
            Assert.NotNull(row);
            Assert.Equal(payments.Sum(), row.PaidAmount);
        });
    }

    // ── Property 3: MarkSettledAsync sets Paid status and records settled_at ──
    // Validates: Requirements 1.3

    [Fact]
    public async Task Property3_MarkSettledAsync_SetsPaidStatusAndNonNullSettledAt()
    {
        // **Validates: Requirements 1.3**
        //
        // For any invoice projection that is not already Paid, calling MarkSettledAsync
        // should return ServiceResult.Success, set Status == "Paid", and set SettledAt != null.

        var gen =
            from invoiceId in Gen.Guid
            from amount in Gen.Decimal[0.01m, 1_000_000m]
            from statusIndex in Gen.Int[0, 2]
            let status = statusIndex switch { 0 => "Draft", 1 => "PartiallyPaid", _ => "Overdue" }
            select (InvoiceId: invoiceId, Amount: amount, Status: status);

        await gen.SampleAsync(async tuple =>
        {
            var (invoiceId, amount, status) = tuple;

            await using var db = CreateDb();
            var svc = new EfCashflowProjectionService(db);

            // Seed the projection row and optionally set a non-Paid status
            await svc.InitialiseAsync(invoiceId, amount, CancellationToken.None);
            if (status != "Draft")
            {
                var row = await db.CashflowProjections.FindAsync(invoiceId);
                row!.Status = status;
                await db.SaveChangesAsync();
            }

            // Act
            var result = await svc.MarkSettledAsync(invoiceId, CancellationToken.None);

            // Assert: returns Success
            Assert.IsType<ServiceResult.Success>(result);

            // Assert: status is Paid and settled_at is set
            var settled = await db.CashflowProjections.FindAsync(invoiceId);
            Assert.NotNull(settled);
            Assert.Equal("Paid", settled.Status);
            Assert.NotNull(settled.SettledAt);
        });
    }

    // ── Property 4: MarkSettledAsync is idempotent when already Paid ──────────
    // Validates: Requirements 5.1

    [Fact]
    public async Task Property4_MarkSettledAsync_IsIdempotentWhenAlreadyPaid()
    {
        // **Validates: Requirements 5.1**
        //
        // For any invoice projection whose Status is already "Paid", calling
        // MarkSettledAsync again should return ServiceResult.Success without
        // modifying the row (same SettledAt, same Status).

        var gen =
            from invoiceId in Gen.Guid
            from amount in Gen.Decimal[0.01m, 1_000_000m]
            select (InvoiceId: invoiceId, Amount: amount);

        await gen.SampleAsync(async tuple =>
        {
            var (invoiceId, amount) = tuple;

            await using var db = CreateDb();
            var svc = new EfCashflowProjectionService(db);

            // Seed and settle the projection
            await svc.InitialiseAsync(invoiceId, amount, CancellationToken.None);
            await svc.MarkSettledAsync(invoiceId, CancellationToken.None);

            // Capture state after first settle
            var afterFirst = await db.CashflowProjections.FindAsync(invoiceId);
            var originalSettledAt = afterFirst!.SettledAt;

            // Act: call MarkSettledAsync a second time
            var result = await svc.MarkSettledAsync(invoiceId, CancellationToken.None);

            // Assert: returns Success
            Assert.IsType<ServiceResult.Success>(result);

            // Assert: row is unchanged
            var afterSecond = await db.CashflowProjections.FindAsync(invoiceId);
            Assert.NotNull(afterSecond);
            Assert.Equal("Paid", afterSecond.Status);
            Assert.Equal(originalSettledAt, afterSecond.SettledAt);
        });
    }

    // ── Property 5: Missing row returns Failure for write operations ──────────
    // Validates: Requirements 1.4, 5.5

    [Fact]
    public async Task Property5_ApplyPaymentAsync_AndMarkOverdueAsync_ReturnFailureForUnknownInvoiceId()
    {
        // **Validates: Requirements 1.4, 5.5**
        //
        // For any invoice ID that does NOT exist in cashflow_projections, calling
        // ApplyPaymentAsync (with positive amount) OR MarkOverdueAsync should
        // return ServiceResult.Failure.

        var gen =
            from invoiceId in Gen.Guid
            from amount in Gen.Decimal[0.01m, 1_000m]
            select (InvoiceId: invoiceId, Amount: amount);

        await gen.SampleAsync(async tuple =>
        {
            var (invoiceId, amount) = tuple;

            await using var db = CreateDb();
            var svc = new EfCashflowProjectionService(db);

            // Act: ApplyPaymentAsync on unknown ID
            var applyResult = await svc.ApplyPaymentAsync(invoiceId, amount, amount, CancellationToken.None);

            // Assert: Failure
            Assert.IsType<ServiceResult.Failure>(applyResult);

            // Act: MarkOverdueAsync on unknown ID
            var overdueResult = await svc.MarkOverdueAsync(invoiceId, CancellationToken.None);

            // Assert: Failure
            Assert.IsType<ServiceResult.Failure>(overdueResult);
        });
    }

    // ── Property 6: Non-positive paymentAmount returns Failure ────────────────
    // Validates: Requirements 1.5

    [Fact]
    public async Task Property6_ApplyPaymentAsync_ReturnsFailureForNonPositivePaymentAmount()
    {
        // **Validates: Requirements 1.5**
        //
        // For any paymentAmount <= 0, calling ApplyPaymentAsync should return
        // ServiceResult.Failure regardless of whether the invoice row exists.

        // Generate non-positive amounts using integer arithmetic to avoid GenDecimal range issues
        var gen = Gen.Select(
            Gen.Guid,
            Gen.Int[0, 10_000],
            Gen.Int[0, 1],
            Gen.Int[0, 1],
            (invoiceId, absAmount, isZero, seedRowInt) =>
                (InvoiceId: invoiceId,
                 Amount: isZero == 1 ? 0m : -(decimal)absAmount,
                 SeedRow: seedRowInt == 1));

        await gen.SampleAsync(async tuple =>
        {
            var (invoiceId, amount, seedRow) = tuple;

            await using var db = CreateDb();
            var svc = new EfCashflowProjectionService(db);

            // Optionally seed a row so we test both "row exists" and "row missing" paths
            if (seedRow)
                await svc.InitialiseAsync(invoiceId, 1000m, CancellationToken.None);

            // Act
            var result = await svc.ApplyPaymentAsync(invoiceId, amount, 0m, CancellationToken.None);

            // Assert: Failure regardless of row existence
            Assert.IsType<ServiceResult.Failure>(result);
        });
    }

    // ── Property 7: Duplicate InitialiseAsync returns Failure ─────────────────
    // Validates: Requirements 2.3

    [Fact]
    public async Task Property7_InitialiseAsync_ReturnsFailureOnSecondCallForSameInvoiceId()
    {
        // **Validates: Requirements 2.3**
        //
        // For any invoice ID that already has a row in cashflow_projections, calling
        // InitialiseAsync a second time should return ServiceResult.Failure.

        var gen =
            from invoiceId in Gen.Guid
            from amount in Gen.Decimal[0.01m, 1_000_000m]
            select (InvoiceId: invoiceId, Amount: amount);

        await gen.SampleAsync(async tuple =>
        {
            var (invoiceId, amount) = tuple;

            await using var db = CreateDb();
            var svc = new EfCashflowProjectionService(db);

            // First call — should succeed
            var firstResult = await svc.InitialiseAsync(invoiceId, amount, CancellationToken.None);
            Assert.IsType<ServiceResult.Success>(firstResult);

            // Act: second call for the same invoice ID
            var secondResult = await svc.InitialiseAsync(invoiceId, amount, CancellationToken.None);

            // Assert: Failure
            Assert.IsType<ServiceResult.Failure>(secondResult);
        });
    }
}

// Feature: cashflow-dashboard, Property 8: query handler computes TotalReceivable, TotalPaid, TotalUnpaid, and OverdueAmount correctly for any set of generated projection rows (including empty set)
// Feature: cashflow-dashboard, Property 9: MonthlyInflow includes only payments whose RecordedAt falls in the current UTC calendar month, for any set of generated payments

using CsCheck;
using Flowmetry.Application.Common;
using Flowmetry.Application.Invoices.Dtos;
using Flowmetry.Application.Invoices.Queries;
using Flowmetry.Domain;
using Flowmetry.Infrastructure;
using Flowmetry.Infrastructure.Persistence;
using Flowmetry.Infrastructure.Projections;
using Microsoft.EntityFrameworkCore;

namespace Flowmetry.API.Tests.PropertyTests;

/// <summary>
/// Property-based tests for GetCashflowDashboardQueryHandler.
/// Property 8: Aggregation metrics computed correctly for any set of projection rows.
/// Property 9: MonthlyInflow sums only current-month payments.
/// </summary>
public class CashflowDashboardQueryPropertyTests
{
    private static readonly string[] AllStatuses = ["Draft", "PartiallyPaid", "Paid", "Overdue"];

    private static FlowmetryDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<FlowmetryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new FlowmetryDbContext(options);
    }

    /// <summary>
    /// Creates a Payment with a specific RecordedAt timestamp using reflection,
    /// since Payment has a private constructor and internal Create method.
    /// </summary>
    private static Payment CreatePaymentWithTimestamp(Guid invoiceId, decimal amount, DateTimeOffset recordedAt)
    {
        // Use reflection to invoke the private constructor
        var payment = (Payment)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Payment));

        var type = typeof(Payment);
        SetPrivateProperty(payment, type, "Id", Guid.NewGuid());
        SetPrivateProperty(payment, type, "InvoiceId", invoiceId);
        SetPrivateProperty(payment, type, "Amount", amount);
        SetPrivateProperty(payment, type, "RecordedAt", recordedAt);

        return payment;
    }

    private static void SetPrivateProperty(object obj, Type type, string propertyName, object value)
    {
        var prop = type.GetProperty(propertyName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        prop!.SetValue(obj, value);
    }

    // ── Property 8: Dashboard aggregation computes all projection-based metrics correctly ──
    // Validates: Requirements 3.1, 3.2, 3.3, 3.5, 3.6

    [Fact]
    public async Task Property8_DashboardQuery_ComputesAllProjectionMetricsCorrectly()
    {
        // **Validates: Requirements 3.1, 3.2, 3.3, 3.5, 3.6**
        //
        // For any set of CashflowProjection rows (0–10) with arbitrary statuses and amounts,
        // the query handler should compute TotalReceivable, TotalPaid, TotalUnpaid, and
        // OverdueAmount correctly — including returning all zeros when the table is empty.

        // Use integer-based generators to avoid decimal overflow in CsCheck
        var rowGen =
            from invoiceId in Gen.Guid
            from invoiceAmountCents in Gen.Int[100, 1_000_000]
            from paidAmountCents in Gen.Int[0, 10_000]
            from statusIndex in Gen.Int[0, 3]
            let invoiceAmount = (decimal)invoiceAmountCents / 100m
            let paidAmount = (decimal)paidAmountCents / 100m
            let status = AllStatuses[statusIndex]
            select new CashflowProjection
            {
                InvoiceId = invoiceId,
                InvoiceAmount = invoiceAmount,
                PaidAmount = paidAmount,
                Status = status,
                SettledAt = status == "Paid" ? DateTimeOffset.UtcNow : null
            };

        var gen = rowGen.List[0, 10];

        await gen.SampleAsync(async rows =>
        {
            await using var db = CreateDb();

            // Seed projection rows
            db.CashflowProjections.AddRange(rows);
            await db.SaveChangesAsync();

            // Compute expected values manually
            var nonPaidStatuses = new[] { "Paid", "Cancelled" };

            var expectedTotalReceivable = rows
                .Where(r => !nonPaidStatuses.Contains(r.Status))
                .Sum(r => r.InvoiceAmount - r.PaidAmount);

            var expectedTotalPaid = rows
                .Where(r => r.Status == "Paid")
                .Sum(r => r.PaidAmount);

            var expectedTotalUnpaid = rows
                .Where(r => !nonPaidStatuses.Contains(r.Status))
                .Sum(r => r.InvoiceAmount);

            var expectedOverdueAmount = rows
                .Where(r => r.Status == "Overdue")
                .Sum(r => r.InvoiceAmount - r.PaidAmount);

            // Act: invoke handler via repository
            var repository = new CashflowDashboardRepository(db);
            var handler = new GetCashflowDashboardQueryHandler(repository);
            var result = await handler.Handle(new GetCashflowDashboardQuery(), CancellationToken.None);

            // Assert
            var success = Assert.IsType<Result<CashflowSummary>.Success>(result);
            var summary = success.Value;

            Assert.Equal(expectedTotalReceivable, summary.TotalReceivable);
            Assert.Equal(expectedTotalPaid, summary.TotalPaid);
            Assert.Equal(expectedTotalUnpaid, summary.TotalUnpaid);
            Assert.Equal(expectedOverdueAmount, summary.OverdueAmount);
        }, iter: 500);
    }

    // ── Property 9: MonthlyInflow sums only current-month payments ────────────
    // Validates: Requirements 3.4

    [Fact]
    public async Task Property9_DashboardQuery_MonthlyInflowIncludesOnlyCurrentMonthPayments()
    {
        // **Validates: Requirements 3.4**
        //
        // For any set of Payment records with arbitrary RecordedAt timestamps,
        // MonthlyInflow should include only those payments whose RecordedAt falls
        // within the current calendar month in UTC, and exclude all others.

        var now = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var monthEnd = monthStart.AddMonths(1);

        // Generator for a payment in the current month (use int-based amounts to avoid decimal overflow)
        var inMonthGen =
            from offsetSeconds in Gen.Long[0L, (long)(monthEnd - monthStart).TotalSeconds - 1]
            from amountCents in Gen.Int[100, 100_000]
            let amount = (decimal)amountCents / 100m
            select (Amount: amount, RecordedAt: monthStart.AddSeconds(offsetSeconds));

        // Generator for a payment outside the current month (past or future)
        var outOfMonthGen =
            from offsetDays in Gen.Int[1, 365]
            from isPast in Gen.Bool
            from amountCents in Gen.Int[100, 100_000]
            let amount = (decimal)amountCents / 100m
            let recordedAt = isPast
                ? monthStart.AddDays(-offsetDays)
                : monthEnd.AddDays(offsetDays)
            select (Amount: amount, RecordedAt: recordedAt);

        var gen =
            from inMonth in inMonthGen.List[0, 5]
            from outOfMonth in outOfMonthGen.List[0, 5]
            select (InMonth: inMonth, OutOfMonth: outOfMonth);

        await gen.SampleAsync(async tuple =>
        {
            var (inMonth, outOfMonth) = tuple;

            await using var db = CreateDb();

            // We need an Invoice to satisfy the FK constraint on payments.
            // Seed a single invoice and attach all payments to it.
            var invoiceId = Guid.NewGuid();
            var invoice = Invoice.Create(invoiceId, 999_999m, DateOnly.FromDateTime(DateTime.Today.AddDays(30)));
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();

            // Seed in-month payments
            foreach (var (amount, recordedAt) in inMonth)
            {
                var payment = CreatePaymentWithTimestamp(invoiceId, amount, recordedAt);
                db.Payments.Add(payment);
            }

            // Seed out-of-month payments
            foreach (var (amount, recordedAt) in outOfMonth)
            {
                var payment = CreatePaymentWithTimestamp(invoiceId, amount, recordedAt);
                db.Payments.Add(payment);
            }

            await db.SaveChangesAsync();

            // Compute expected MonthlyInflow
            var expectedMonthlyInflow = inMonth.Sum(p => p.Amount);

            // Act
            var repository = new CashflowDashboardRepository(db);
            var handler = new GetCashflowDashboardQueryHandler(repository);
            var result = await handler.Handle(new GetCashflowDashboardQuery(), CancellationToken.None);

            // Assert
            var success = Assert.IsType<Result<CashflowSummary>.Success>(result);
            Assert.Equal(expectedMonthlyInflow, success.Value.MonthlyInflow);
        }, iter: 500);
    }
}

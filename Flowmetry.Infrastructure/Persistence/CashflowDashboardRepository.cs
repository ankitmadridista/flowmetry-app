using Flowmetry.Application.Invoices;
using Flowmetry.Application.Invoices.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Flowmetry.Infrastructure.Persistence;

public class CashflowDashboardRepository(FlowmetryDbContext db) : ICashflowDashboardRepository
{
    public async Task<CashflowSummary> GetSummaryAsync(CancellationToken ct)
    {
        var nonPaidStatuses = new[] { "Paid", "Cancelled" };

        var totalReceivable = await db.CashflowProjections
            .Where(p => !nonPaidStatuses.Contains(p.Status))
            .SumAsync(p => p.InvoiceAmount - p.PaidAmount, ct);

        var totalPaid = await db.CashflowProjections
            .Where(p => p.Status == "Paid")
            .SumAsync(p => p.PaidAmount, ct);

        var totalUnpaid = await db.CashflowProjections
            .Where(p => !nonPaidStatuses.Contains(p.Status))
            .SumAsync(p => p.InvoiceAmount, ct);

        var overdueAmount = await db.CashflowProjections
            .Where(p => p.Status == "Overdue")
            .SumAsync(p => p.InvoiceAmount - p.PaidAmount, ct);

        var now = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var monthEnd = monthStart.AddMonths(1);

        var monthlyInflow = await db.Payments
            .Where(p => p.RecordedAt >= monthStart && p.RecordedAt < monthEnd)
            .SumAsync(p => p.Amount, ct);

        return new CashflowSummary(
            TotalReceivable: totalReceivable,
            TotalPaid: totalPaid,
            TotalUnpaid: totalUnpaid,
            MonthlyInflow: monthlyInflow,
            OverdueAmount: overdueAmount);
    }
}

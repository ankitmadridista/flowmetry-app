using Flowmetry.Application.Common;
using Flowmetry.Application.Invoices.Services;
using Microsoft.EntityFrameworkCore;

namespace Flowmetry.Infrastructure.Projections;

public class EfCashflowProjectionService(FlowmetryDbContext db) : ICashflowProjectionService
{
    public async Task<ServiceResult> InitialiseAsync(Guid invoiceId, decimal invoiceAmount, CancellationToken ct)
    {
        var exists = await db.CashflowProjections.AnyAsync(p => p.InvoiceId == invoiceId, ct);
        if (exists)
            return new ServiceResult.Failure($"Cashflow projection for invoice {invoiceId} already exists.");

        db.CashflowProjections.Add(new CashflowProjection
        {
            InvoiceId = invoiceId,
            InvoiceAmount = invoiceAmount,
            PaidAmount = 0,
            Status = "Draft"
        });

        await db.SaveChangesAsync(ct);
        return new ServiceResult.Success();
    }

    public async Task<ServiceResult> ApplyPaymentAsync(Guid invoiceId, decimal paymentAmount, decimal runningTotal, CancellationToken ct)
    {
        if (paymentAmount <= 0)
            return new ServiceResult.Failure($"Payment amount must be greater than zero, got {paymentAmount}.");

        var projection = await db.CashflowProjections.FindAsync([invoiceId], ct);
        if (projection is null)
            return new ServiceResult.Failure($"Cashflow projection for invoice {invoiceId} not found.");

        projection.PaidAmount += paymentAmount;
        projection.Status = "PartiallyPaid";

        await db.SaveChangesAsync(ct);
        return new ServiceResult.Success();
    }

    public async Task<ServiceResult> MarkSettledAsync(Guid invoiceId, CancellationToken ct)
    {
        var projection = await db.CashflowProjections.FindAsync([invoiceId], ct);
        if (projection is null)
            return new ServiceResult.Failure($"Cashflow projection for invoice {invoiceId} not found.");

        if (projection.Status == "Paid")
            return new ServiceResult.Success();

        projection.Status = "Paid";
        projection.SettledAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return new ServiceResult.Success();
    }

    public async Task<ServiceResult> MarkOverdueAsync(Guid invoiceId, CancellationToken ct)
    {
        var projection = await db.CashflowProjections.FindAsync([invoiceId], ct);
        if (projection is null)
            return new ServiceResult.Failure($"Cashflow projection for invoice {invoiceId} not found.");

        projection.Status = "Overdue";

        await db.SaveChangesAsync(ct);
        return new ServiceResult.Success();
    }
}

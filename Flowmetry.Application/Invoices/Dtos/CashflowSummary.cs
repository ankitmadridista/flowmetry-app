namespace Flowmetry.Application.Invoices.Dtos;

public record CashflowSummary(
    decimal TotalReceivable,
    decimal TotalPaid,
    decimal TotalUnpaid,
    decimal MonthlyInflow,
    decimal OverdueAmount);

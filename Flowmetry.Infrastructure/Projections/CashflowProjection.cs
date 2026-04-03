namespace Flowmetry.Infrastructure.Projections;

public class CashflowProjection
{
    public Guid InvoiceId { get; set; }
    public decimal InvoiceAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTimeOffset? SettledAt { get; set; }
}

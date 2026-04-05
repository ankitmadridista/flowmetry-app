namespace Flowmetry.Domain;

public record RiskProfile(
    int RiskScore,
    RiskBand RiskBand,
    int TotalInvoices,
    int OverdueCount,
    int PartiallyPaidCount,
    int LatePaymentCount,
    double AverageDaysLate);

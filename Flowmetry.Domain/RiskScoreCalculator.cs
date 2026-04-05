namespace Flowmetry.Domain;

public static class RiskScoreCalculator
{
    private const double OverdueWeight       = 0.40;
    private const double PartiallyPaidWeight = 0.25;
    private const double LatePaymentWeight   = 0.20;
    private const double AvgDaysLateWeight   = 0.15;
    private const double MaxAvgDaysLate      = 60.0;

    public static RiskProfile Calculate(IReadOnlyList<Invoice> invoices)
    {
        var nonCancelled = invoices
            .Where(i => i.Status != InvoiceStatus.Cancelled)
            .ToList();

        int total = nonCancelled.Count;

        if (total == 0)
            return new RiskProfile(0, RiskBand.Low, 0, 0, 0, 0, 0.0);

        int overdueCount       = nonCancelled.Count(i => i.Status == InvoiceStatus.Overdue);
        int partiallyPaidCount = nonCancelled.Count(i => i.Status == InvoiceStatus.PartiallyPaid);

        var latePaymentDays = nonCancelled
            .SelectMany(i => i.Payments.Select(p => new
            {
                DaysLate = (p.RecordedAt.Date - i.DueDate.ToDateTime(TimeOnly.MinValue).Date).TotalDays
            }))
            .Where(x => x.DaysLate > 0)
            .Select(x => x.DaysLate)
            .ToList();

        int latePaymentCount = latePaymentDays.Count;
        double avgDaysLate   = latePaymentCount > 0 ? latePaymentDays.Average() : 0.0;

        double overdueRatio       = (double)overdueCount       / total;
        double partiallyPaidRatio = (double)partiallyPaidCount / total;
        double latePaymentRatio   = (double)latePaymentCount   / total;
        double normAvgDays        = Math.Min(avgDaysLate, MaxAvgDaysLate) / MaxAvgDaysLate;

        double rawScore = (overdueRatio       * OverdueWeight
                         + partiallyPaidRatio * PartiallyPaidWeight
                         + latePaymentRatio   * LatePaymentWeight
                         + normAvgDays        * AvgDaysLateWeight) * 100.0;

        int riskScore = Math.Clamp((int)Math.Round(rawScore), 0, 100);
        RiskBand band = ClassifyBand(riskScore);

        return new RiskProfile(
            riskScore,
            band,
            total,
            overdueCount,
            partiallyPaidCount,
            latePaymentCount,
            avgDaysLate);
    }

    private static RiskBand ClassifyBand(int score) => score switch
    {
        <= 30 => RiskBand.Low,
        <= 65 => RiskBand.Medium,
        _     => RiskBand.High
    };
}

// Feature: cashflow-dashboard, Property 12: CashflowSummary serialises to JSON and deserialises back to an equivalent object for any valid decimal field values

using System.Text.Json;
using CsCheck;
using Flowmetry.Application.Invoices.Dtos;

namespace Flowmetry.API.Tests.PropertyTests;

/// <summary>
/// Property-based tests for CashflowSummary serialisation.
/// Property 12: Round-trip serialisation produces an equivalent object.
/// </summary>
public class CashflowSerialisationPropertyTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ── Property 12: CashflowSummary round-trip serialisation ────────────────
    // Validates: Requirements 7.2

    [Fact]
    public void Property12_CashflowSummary_RoundTripSerialisation()
    {
        // **Validates: Requirements 7.2**
        //
        // For any valid CashflowSummary with arbitrary decimal field values,
        // serialising to JSON and then deserialising should produce an object
        // equal to the original.

        // Use int-based generators to avoid decimal overflow/precision issues.
        var gen =
            from totalReceivable in Gen.Int[0, 1_000_000].Select(i => (decimal)i / 100m)
            from totalPaid in Gen.Int[0, 1_000_000].Select(i => (decimal)i / 100m)
            from totalUnpaid in Gen.Int[0, 1_000_000].Select(i => (decimal)i / 100m)
            from monthlyInflow in Gen.Int[0, 1_000_000].Select(i => (decimal)i / 100m)
            from overdueAmount in Gen.Int[0, 1_000_000].Select(i => (decimal)i / 100m)
            select new CashflowSummary(totalReceivable, totalPaid, totalUnpaid, monthlyInflow, overdueAmount);

        gen.Sample(original =>
        {
            var json = JsonSerializer.Serialize(original, JsonOptions);
            var deserialised = JsonSerializer.Deserialize<CashflowSummary>(json, JsonOptions);

            Assert.NotNull(deserialised);
            Assert.Equal(original.TotalReceivable, deserialised!.TotalReceivable);
            Assert.Equal(original.TotalPaid, deserialised.TotalPaid);
            Assert.Equal(original.TotalUnpaid, deserialised.TotalUnpaid);
            Assert.Equal(original.MonthlyInflow, deserialised.MonthlyInflow);
            Assert.Equal(original.OverdueAmount, deserialised.OverdueAmount);
        });
    }
}

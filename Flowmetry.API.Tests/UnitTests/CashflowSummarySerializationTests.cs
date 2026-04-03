using System.Text.Json;
using Flowmetry.Application.Invoices.Dtos;

namespace Flowmetry.API.Tests.UnitTests;

public class CashflowSummarySerializationTests
{
    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // 10.3: CashflowSummary JSON keys are camelCase
    [Fact]
    public void Serialize_CashflowSummary_ProducesCamelCaseKeys()
    {
        var summary = new CashflowSummary(
            TotalReceivable: 100m,
            TotalPaid: 200m,
            TotalUnpaid: 300m,
            MonthlyInflow: 400m,
            OverdueAmount: 500m);

        var json = JsonSerializer.Serialize(summary, CamelCaseOptions);

        Assert.Contains("\"totalReceivable\"", json);
        Assert.Contains("\"totalPaid\"", json);
        Assert.Contains("\"totalUnpaid\"", json);
        Assert.Contains("\"monthlyInflow\"", json);
        Assert.Contains("\"overdueAmount\"", json);
    }

    // 10.4: Deserialising CashflowSummary from JSON with unknown extra fields produces a valid object without error
    [Fact]
    public void Deserialize_CashflowSummaryWithUnknownFields_IgnoresExtraFieldsAndProducesValidObject()
    {
        const string json = """
            {
                "totalReceivable": 1.0,
                "totalPaid": 2.0,
                "totalUnpaid": 3.0,
                "monthlyInflow": 4.0,
                "overdueAmount": 5.0,
                "unknownField": "ignored"
            }
            """;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        var summary = JsonSerializer.Deserialize<CashflowSummary>(json, options);

        Assert.NotNull(summary);
        Assert.Equal(1.0m, summary.TotalReceivable);
        Assert.Equal(2.0m, summary.TotalPaid);
        Assert.Equal(3.0m, summary.TotalUnpaid);
        Assert.Equal(4.0m, summary.MonthlyInflow);
        Assert.Equal(5.0m, summary.OverdueAmount);
    }
}

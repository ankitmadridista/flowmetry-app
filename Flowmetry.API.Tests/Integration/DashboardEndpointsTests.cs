using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Flowmetry.API.Tests.Integration;

public class DashboardEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DashboardEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = TestAuthHelper.CreateAuthenticatedClient(factory.CreateClient());
    }

    // 10.1: GET /api/dashboard/cashflow returns HTTP 200 with all five camelCase decimal fields
    [Fact]
    public async Task GetCashflow_Returns200WithAllFiveCamelCaseFields()
    {
        var response = await _client.GetAsync("/api/dashboard/cashflow");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(json.TryGetProperty("totalReceivable", out _),   "Missing field: totalReceivable");
        Assert.True(json.TryGetProperty("totalPaid", out _),         "Missing field: totalPaid");
        Assert.True(json.TryGetProperty("totalUnpaid", out _),       "Missing field: totalUnpaid");
        Assert.True(json.TryGetProperty("monthlyInflow", out _),     "Missing field: monthlyInflow");
        Assert.True(json.TryGetProperty("overdueAmount", out _),     "Missing field: overdueAmount");
    }

    // 10.2: endpoint returns HTTP 200 with all-zero fields when no invoices exist
    [Fact]
    public async Task GetCashflow_EmptyDatabase_Returns200WithAllZeroFields()
    {
        var response = await _client.GetAsync("/api/dashboard/cashflow");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(0m, json.GetProperty("totalReceivable").GetDecimal());
        Assert.Equal(0m, json.GetProperty("totalPaid").GetDecimal());
        Assert.Equal(0m, json.GetProperty("totalUnpaid").GetDecimal());
        Assert.Equal(0m, json.GetProperty("monthlyInflow").GetDecimal());
        Assert.Equal(0m, json.GetProperty("overdueAmount").GetDecimal());
    }
}

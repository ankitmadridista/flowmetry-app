using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Flowmetry.API.Tests.Integration;

// ── Tests ─────────────────────────────────────────────────────────────────────

public class PermissionsEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PermissionsEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // 9.1: Unauthenticated request to GET /api/auth/permissions returns 401
    // Validates: Requirements 4.2
    [Fact]
    public async Task GetPermissions_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        // No Authorization header — request is unauthenticated

        var response = await client.GetAsync("/api/auth/permissions");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // 9.2: Authenticated request returns 200 with correct JSON shape
    // Validates: Requirements 4.3
    [Fact]
    public async Task GetPermissions_Authenticated_Returns200WithCorrectShape()
    {
        var client = TestAuthHelper.CreateAuthenticatedClient(_factory.CreateClient());

        var response = await client.GetAsync("/api/auth/permissions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Must contain securityObjectStatus (object with boolean values)
        Assert.True(json.TryGetProperty("securityObjectStatus", out var statusProp),
            "Response missing 'securityObjectStatus' property");
        Assert.Equal(JsonValueKind.Object, statusProp.ValueKind);

        // Must contain permissions (array of integers)
        Assert.True(json.TryGetProperty("permissions", out var permsProp),
            "Response missing 'permissions' property");
        Assert.Equal(JsonValueKind.Array, permsProp.ValueKind);
    }
}

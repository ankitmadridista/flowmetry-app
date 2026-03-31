using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Flowmetry.API.Tests;

public class ApiIntegrationTests
{
    // Factory that injects a fake DATABASE_URL so startup doesn't throw
    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DATABASE_URL", "Host=localhost;Database=test;Username=test;Password=test");
        });

    [Fact]
    public async Task HealthEndpoint_Returns200WithHealthyBody()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var status = doc.RootElement.GetProperty("status").GetString();
        Assert.Equal("healthy", status);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsCorsHeaderForLocalhostOrigin()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "http://localhost:5173");

        var response = await client.SendAsync(request);

        Assert.True(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "Expected Access-Control-Allow-Origin header to be present");

        var allowOrigin = response.Headers.GetValues("Access-Control-Allow-Origin").FirstOrDefault();
        Assert.Equal("http://localhost:5173", allowOrigin);
    }

    [Fact]
    public async Task MissingDatabaseUrl_ThrowsInvalidOperationExceptionAtStartup()
    {
        // Save and clear the env var so the fallback also fails
        var original = Environment.GetEnvironmentVariable("DATABASE_URL");
        Environment.SetEnvironmentVariable("DATABASE_URL", null);

        try
        {
            var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                // Remove DATABASE_URL from configuration by overriding with an empty in-memory source
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["DATABASE_URL"] = null
                    });
                });
            });

            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await factory.CreateClient().GetAsync("/health"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("DATABASE_URL", original);
        }
    }
}

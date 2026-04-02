using System.Text.Json;
using Flowmetry.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowmetry.API.Tests;

public class ApiIntegrationTests
{
    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DATABASE_URL", "Host=localhost;Database=test;Username=test;Password=test");
            builder.ConfigureServices(services =>
            {
                // Remove Npgsql registrations and replace with InMemory
                var contextType = typeof(FlowmetryDbContext);
                var optionsConfigType = typeof(IDbContextOptionsConfiguration<>).MakeGenericType(contextType);
                var toRemove = services
                    .Where(d =>
                        d.ServiceType == typeof(DbContextOptions<FlowmetryDbContext>) ||
                        d.ServiceType == typeof(DbContextOptions) ||
                        d.ServiceType == contextType ||
                        d.ServiceType == optionsConfigType ||
                        (d.ServiceType.IsGenericType &&
                         d.ServiceType.GetGenericArguments().Any(a => a == contextType)))
                    .ToList();
                foreach (var d in toRemove) services.Remove(d);

                services.AddDbContext<FlowmetryDbContext>(options =>
                    options.UseInMemoryDatabase($"TestDb-{Guid.NewGuid()}"));
            });
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
        Assert.Equal("healthy", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsCorsHeaderForLocalhostOrigin()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "http://localhost:5173");

        var response = await client.SendAsync(request);

        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal("http://localhost:5173",
            response.Headers.GetValues("Access-Control-Allow-Origin").FirstOrDefault());
    }

}

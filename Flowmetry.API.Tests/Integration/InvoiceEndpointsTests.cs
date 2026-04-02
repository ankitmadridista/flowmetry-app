using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Flowmetry.Application.Common;
using Flowmetry.Application.Invoices.Commands;
using Flowmetry.Domain;
using Flowmetry.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Flowmetry.API.Tests.Integration;

// ── Factory ──────────────────────────────────────────────────────────────────

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public static readonly Guid SeededCustomerId = Guid.NewGuid();

    // Unique DB name per factory instance so tests don't share state
    private readonly string _dbName = $"TestDb-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Provide a fake DATABASE_URL so AddInfrastructure doesn't throw
        builder.UseSetting("DATABASE_URL", "Host=localhost;Database=test;Username=test;Password=test");

        builder.ConfigureServices(services =>
        {
            // Remove ALL DbContext-related registrations to avoid dual-provider conflict
            RemoveDbContextRegistrations(services);

            // Add InMemory DbContext instead
            services.AddDbContext<FlowmetryDbContext>(options =>
                options.UseInMemoryDatabase(_dbName)
                       .EnableSensitiveDataLogging());
        });
    }

    private static void RemoveDbContextRegistrations(IServiceCollection services)
    {
        // In EF Core 9, AddDbContext registers IDbContextOptionsConfiguration<TContext>
        // descriptors that configure the provider. We must remove ALL of them for
        // FlowmetryDbContext to avoid the "two providers registered" error.
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

        foreach (var d in toRemove)
            services.Remove(d);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }

    /// <summary>Seed the database and return the factory (fluent).</summary>
    public CustomWebApplicationFactory WithSeededData()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowmetryDbContext>();
        db.Database.EnsureCreated();
        SeedCustomer(db);
        return this;
    }

    private static void SeedCustomer(FlowmetryDbContext db)
    {
        if (db.Customers.Any(c => c.Id == SeededCustomerId)) return;

        // Customer has a private constructor — create via uninitialized object + reflection
        var customer = (Customer)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(Customer));

        typeof(Customer).GetProperty("Id")!.SetValue(customer, SeededCustomerId);
        typeof(Customer).GetProperty("Name")!.SetValue(customer, "Test Customer");
        typeof(Customer).GetProperty("Email")!.SetValue(customer, "test@example.com");
        typeof(Customer).GetProperty("RiskScore")!.SetValue(customer, 1);

        db.Customers.Add(customer);
        db.SaveChanges();
    }

    /// <summary>Dispatch a MediatR command using the factory's service scope.</summary>
    public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        using var scope = Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(request);
    }
}

// ── Tests ─────────────────────────────────────────────────────────────────────

public class InvoiceEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private static readonly Guid CustomerId = CustomWebApplicationFactory.SeededCustomerId;

    public InvoiceEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.WithSeededData();
        _client = factory.CreateClient();
    }

    // ── 12.1: POST /api/invoices ──────────────────────────────────────────────

    [Fact]
    public async Task PostInvoice_ValidRequest_Returns201WithId()
    {
        var body = new
        {
            customerId = CustomerId,
            amount = 500m,
            dueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)).ToString("yyyy-MM-dd")
        };

        var response = await _client.PostAsJsonAsync("/api/invoices", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("id", out var idProp));
        Assert.True(Guid.TryParse(idProp.GetString(), out _));
    }

    [Fact]
    public async Task PostInvoice_AmountZero_Returns400()
    {
        var body = new
        {
            customerId = CustomerId,
            amount = 0m,
            dueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)).ToString("yyyy-MM-dd")
        };

        var response = await _client.PostAsJsonAsync("/api/invoices", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostInvoice_NegativeAmount_Returns400()
    {
        var body = new
        {
            customerId = CustomerId,
            amount = -100m,
            dueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)).ToString("yyyy-MM-dd")
        };

        var response = await _client.PostAsJsonAsync("/api/invoices", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostInvoice_PastDueDate_Returns400()
    {
        var body = new
        {
            customerId = CustomerId,
            amount = 100m,
            dueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-1)).ToString("yyyy-MM-dd")
        };

        var response = await _client.PostAsJsonAsync("/api/invoices", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── 12.2: POST /api/invoices/{id}/payments ────────────────────────────────

    [Fact]
    public async Task PostPayment_ValidRequest_Returns200()
    {
        var invoiceId = await CreateSentInvoiceAsync(200m);

        var response = await _client.PostAsJsonAsync(
            $"/api/invoices/{invoiceId}/payments",
            new { amount = 50m });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostPayment_InvalidAmount_Returns400()
    {
        var invoiceId = await CreateSentInvoiceAsync(200m);

        var response = await _client.PostAsJsonAsync(
            $"/api/invoices/{invoiceId}/payments",
            new { amount = 0m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostPayment_MissingInvoice_Returns404()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/invoices/{Guid.NewGuid()}/payments",
            new { amount = 50m });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── 12.3: GET /api/invoices/{id} ─────────────────────────────────────────

    [Fact]
    public async Task GetInvoice_ExistingId_Returns200WithDto()
    {
        var invoiceId = await CreateInvoiceAsync(300m);

        var response = await _client.GetAsync($"/api/invoices/{invoiceId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(invoiceId.ToString(), json.GetProperty("id").GetString());
        Assert.True(json.TryGetProperty("payments", out _));
    }

    [Fact]
    public async Task GetInvoice_MissingId_Returns404()
    {
        var response = await _client.GetAsync($"/api/invoices/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── 12.4: GET /api/invoices ───────────────────────────────────────────────

    [Fact]
    public async Task GetInvoices_OverdueFilterTrue_ReturnsOnlyOverdueInvoices()
    {
        var response = await _client.GetAsync("/api/invoices?overdue=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, json.ValueKind);

        // All returned invoices must have Status == "Overdue"
        foreach (var item in json.EnumerateArray())
        {
            Assert.Equal("Overdue", item.GetProperty("status").GetString());
        }
    }

    [Fact]
    public async Task GetInvoices_OverdueFilterFalse_ReturnsAllInvoices()
    {
        // Create at least one invoice so the list is non-empty
        await CreateInvoiceAsync(100m);

        var response = await _client.GetAsync("/api/invoices?overdue=false");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, json.ValueKind);
        Assert.True(json.GetArrayLength() >= 1);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Guid> CreateInvoiceAsync(decimal amount)
    {
        var result = await _factory.SendAsync(
            new CreateInvoiceCommand(CustomerId, amount,
                DateOnly.FromDateTime(DateTime.Today.AddDays(30))));

        var success = Assert.IsType<Result<Guid>.Success>(result);
        return success.Value;
    }

    private async Task<Guid> CreateSentInvoiceAsync(decimal amount)
    {
        var invoiceId = await CreateInvoiceAsync(amount);
        // Use MediatR pipeline to send the invoice (same as production code path)
        await _factory.SendAsync(new SendInvoiceCommand(invoiceId));
        return invoiceId;
    }
}

using Flowmetry.Application;
using Flowmetry.Application.Common;
using Flowmetry.Application.Invoices;
using Flowmetry.Application.Invoices.Commands;
using Flowmetry.Application.Invoices.Dtos;
using Flowmetry.Domain;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Flowmetry.API.Tests.Behaviors;

/// <summary>
/// Tests that ValidationBehavior short-circuits before the handler is invoked
/// when the command is invalid (Task 12.5).
/// </summary>
public class PipelineBehaviorTests
{
    private static IServiceProvider BuildPipeline(IInvoiceRepository repository)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        // Replace the repository with the provided one
        services.AddScoped<IInvoiceRepository>(_ => repository);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task ValidationBehavior_InvalidAmount_ShortCircuitsBeforeHandler()
    {
        var trackingRepo = new TrackingInvoiceRepository();
        var sp = BuildPipeline(trackingRepo);

        using var scope = sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Amount <= 0 is invalid — ValidationBehavior should short-circuit
        var command = new CreateInvoiceCommand(
            Guid.NewGuid(),
            -50m,
            DateOnly.FromDateTime(DateTime.Today.AddDays(10)));

        var result = await mediator.Send(command);

        // Result must be ValidationFailure
        Assert.IsType<Result<Guid>.ValidationFailure>(result);

        // Handler must NOT have been invoked — no invoice persisted
        Assert.False(trackingRepo.AddWasCalled, "Handler should not have been invoked for invalid command.");
    }

    [Fact]
    public async Task ValidationBehavior_PastDueDate_ShortCircuitsBeforeHandler()
    {
        var trackingRepo = new TrackingInvoiceRepository();
        var sp = BuildPipeline(trackingRepo);

        using var scope = sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var command = new CreateInvoiceCommand(
            Guid.NewGuid(),
            100m,
            DateOnly.FromDateTime(DateTime.Today.AddDays(-5)));

        var result = await mediator.Send(command);

        Assert.IsType<Result<Guid>.ValidationFailure>(result);
        Assert.False(trackingRepo.AddWasCalled);
    }

    [Fact]
    public async Task ValidationBehavior_ValidCommand_InvokesHandler()
    {
        var trackingRepo = new TrackingInvoiceRepository(customerExists: true);
        var sp = BuildPipeline(trackingRepo);

        using var scope = sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var command = new CreateInvoiceCommand(
            Guid.NewGuid(),
            100m,
            DateOnly.FromDateTime(DateTime.Today.AddDays(10)));

        var result = await mediator.Send(command);

        // Handler was invoked (customer exists → Success)
        Assert.IsType<Result<Guid>.Success>(result);
        Assert.True(trackingRepo.AddWasCalled);
    }
}

// ── In-memory tracking repository ────────────────────────────────────────────

internal class TrackingInvoiceRepository(bool customerExists = false) : IInvoiceRepository
{
    private readonly List<Invoice> _invoices = new();

    public bool AddWasCalled { get; private set; }

    public Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_invoices.FirstOrDefault(i => i.Id == id));

    public Task<Invoice?> GetByIdWithPaymentsAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_invoices.FirstOrDefault(i => i.Id == id));

    public Task<IReadOnlyList<Invoice>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Invoice>>(_invoices.AsReadOnly());

    public Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        AddWasCalled = true;
        _invoices.Add(invoice);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken cancellationToken = default)
        => Task.FromResult(customerExists);

    public Task<PagedResult<Invoice>> GetPagedAsync(InvoiceFilter filter, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<PagedResult<InvoiceSummaryDto>> GetPagedSummariesAsync(InvoiceFilter filter, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<InvoiceDetailsDto?> GetDetailsByIdAsync(Guid id, CancellationToken ct = default)
        => throw new NotImplementedException();
}

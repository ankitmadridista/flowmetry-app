// Feature: domain-event-handlers
// Unit tests for edge cases and error conditions in event handlers

using Flowmetry.Application.Common;
using Flowmetry.Application.Invoices;
using Flowmetry.Application.Invoices.EventHandlers;
using Flowmetry.Application.Invoices.Services;
using Flowmetry.Domain;
using Flowmetry.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Flowmetry.API.Tests.UnitTests;

/// <summary>
/// Unit tests for edge cases and error conditions in domain event handlers.
/// </summary>
public class EventHandlerEdgeCaseTests
{
    // ── 11.1: InvoiceSentHandler logs warning and returns when invoice not found ──
    // Requirements: 3.3

    [Fact]
    public async Task InvoiceSentHandler_WhenInvoiceNotFound_LogsWarningAndMakesNoSchedulerCalls()
    {
        // Arrange
        var invoiceId = Guid.NewGuid();
        var repo = new NullReturningRepository();
        var scheduler = new RecordingScheduler();
        var logger = new CapturingLogger<InvoiceSentHandler>();
        var options = Options.Create(new ReminderOptions { DueDateReminderDaysBeforeDue = 3 });

        var evt = new InvoiceSent(invoiceId, DateTimeOffset.UtcNow);
        var notification = new DomainEventNotification<InvoiceSent>(evt);

        var handler = new InvoiceSentHandler(repo, scheduler, options, logger);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert: no scheduler calls were made
        Assert.Empty(scheduler.ScheduleCalls);
        Assert.Empty(scheduler.CancelOrReplaceCalls);

        // Assert: a warning was logged containing the InvoiceId
        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains(invoiceId.ToString(), warning.Message);
    }

    // ── 11.2: Handlers log warning and do not rethrow when service returns Failure ──
    // Requirements: 2.3, 4.3, 5.3, 5.4

    [Fact]
    public async Task InvoiceCreatedHandler_WhenSchedulerReturnsFail_LogsWarningAndDoesNotRethrow()
    {
        // Arrange
        var invoiceId = Guid.NewGuid();
        var scheduler = new FailingScheduler();
        var logger = new CapturingLogger<InvoiceCreatedHandler>();
        var options = Options.Create(new ReminderOptions { InitialReminderDaysBeforeDue = 7 });

        var evt = new InvoiceCreated(invoiceId, Guid.NewGuid(), 500m, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));
        var notification = new DomainEventNotification<InvoiceCreated>(evt);

        var handler = new InvoiceCreatedHandler(scheduler, options, new NoOpCashflowService(), logger);

        // Act — must not throw
        await handler.Handle(notification, CancellationToken.None);

        // Assert: a warning was logged containing the InvoiceId and failure reason
        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains(invoiceId.ToString(), warning.Message);
        Assert.Contains("scheduler error", warning.Message);
    }

    [Fact]
    public async Task PaymentReceivedHandler_WhenCashflowServiceReturnsFail_LogsWarningAndDoesNotRethrow()
    {
        // Arrange
        var invoiceId = Guid.NewGuid();
        var repo = new NullReturningRepository();
        var cashflow = new FailingCashflowService();
        var logger = new CapturingLogger<PaymentReceivedHandler>();

        var evt = new PaymentReceived(invoiceId, 100m, 100m);
        var notification = new DomainEventNotification<PaymentReceived>(evt);

        var handler = new PaymentReceivedHandler(repo, cashflow, logger);

        // Act — must not throw
        await handler.Handle(notification, CancellationToken.None);

        // Assert: a warning was logged
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task InvoiceOverdueHandler_WhenAlertServiceReturnsFail_LogsWarningAndContinuesToScheduleEscalation()
    {
        // Arrange
        var invoiceId = Guid.NewGuid();
        var alertService = new FailingAlertService();
        var scheduler = new RecordingScheduler();
        var logger = new CapturingLogger<InvoiceOverdueHandler>();
        var options = Options.Create(new ReminderOptions { EscalationReminderDaysAfterDue = 1 });

        var evt = new InvoiceOverdue(invoiceId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));
        var notification = new DomainEventNotification<InvoiceOverdue>(evt);

        var handler = new InvoiceOverdueHandler(alertService, scheduler, options, new NoOpCashflowService(), logger);

        // Act — must not throw
        await handler.Handle(notification, CancellationToken.None);

        // Assert: a warning was logged for the alert failure
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);

        // Assert: CancelOrReplaceAsync was still called (handler continues after alert failure)
        Assert.Single(scheduler.CancelOrReplaceCalls);
    }

    [Fact]
    public async Task InvoiceOverdueHandler_WhenSchedulerReturnsFail_LogsWarningAndDoesNotRethrow()
    {
        // Arrange
        var invoiceId = Guid.NewGuid();
        var alertService = new SucceedingAlertService();
        var scheduler = new FailingScheduler();
        var logger = new CapturingLogger<InvoiceOverdueHandler>();
        var options = Options.Create(new ReminderOptions { EscalationReminderDaysAfterDue = 1 });

        var evt = new InvoiceOverdue(invoiceId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));
        var notification = new DomainEventNotification<InvoiceOverdue>(evt);

        var handler = new InvoiceOverdueHandler(alertService, scheduler, options, new NoOpCashflowService(), logger);

        // Act — must not throw
        await handler.Handle(notification, CancellationToken.None);

        // Assert: a warning was logged for the scheduler failure
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
    }
}

// ── NullReturningRepository ───────────────────────────────────────────────────

internal class NullReturningRepository : IInvoiceRepository
{
    public Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult<Invoice?>(null);

    public Task<Invoice?> GetByIdWithPaymentsAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<IReadOnlyList<Invoice>> GetAllAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}

// ── RecordingScheduler ────────────────────────────────────────────────────────

internal class RecordingScheduler : IReminderScheduler
{
    public record ScheduleCall(Guid InvoiceId, ReminderType Type, DateTimeOffset ScheduledAt);

    public List<ScheduleCall> ScheduleCalls { get; } = new();
    public List<ScheduleCall> CancelOrReplaceCalls { get; } = new();

    public Task<ServiceResult> ScheduleAsync(Guid invoiceId, ReminderType type, DateTimeOffset scheduledAt, CancellationToken ct = default)
    {
        ScheduleCalls.Add(new(invoiceId, type, scheduledAt));
        return Task.FromResult<ServiceResult>(new ServiceResult.Success());
    }

    public Task<ServiceResult> CancelOrReplaceAsync(Guid invoiceId, ReminderType type, DateTimeOffset scheduledAt, CancellationToken ct = default)
    {
        CancelOrReplaceCalls.Add(new(invoiceId, type, scheduledAt));
        return Task.FromResult<ServiceResult>(new ServiceResult.Success());
    }
}

// ── CapturingLogger<T> ────────────────────────────────────────────────────────

internal class CapturingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Entries.Add((logLevel, formatter(state, exception)));
    }
}

// ── FailingScheduler ──────────────────────────────────────────────────────────

internal class FailingScheduler : IReminderScheduler
{
    public Task<ServiceResult> ScheduleAsync(Guid invoiceId, ReminderType type, DateTimeOffset scheduledAt, CancellationToken ct = default)
        => Task.FromResult<ServiceResult>(new ServiceResult.Failure("scheduler error"));

    public Task<ServiceResult> CancelOrReplaceAsync(Guid invoiceId, ReminderType type, DateTimeOffset scheduledAt, CancellationToken ct = default)
        => Task.FromResult<ServiceResult>(new ServiceResult.Failure("scheduler error"));
}

// ── FailingCashflowService ────────────────────────────────────────────────────

internal class FailingCashflowService : ICashflowProjectionService
{
    public Task<ServiceResult> InitialiseAsync(Guid invoiceId, decimal invoiceAmount, CancellationToken ct)
        => Task.FromResult<ServiceResult>(new ServiceResult.Failure("cashflow error"));

    public Task<ServiceResult> ApplyPaymentAsync(Guid invoiceId, decimal paymentAmount, decimal runningTotal, CancellationToken ct = default)
        => Task.FromResult<ServiceResult>(new ServiceResult.Failure("cashflow error"));

    public Task<ServiceResult> MarkSettledAsync(Guid invoiceId, CancellationToken ct = default)
        => Task.FromResult<ServiceResult>(new ServiceResult.Failure("cashflow error"));

    public Task<ServiceResult> MarkOverdueAsync(Guid invoiceId, CancellationToken ct)
        => Task.FromResult<ServiceResult>(new ServiceResult.Failure("cashflow error"));
}

// ── FailingAlertService ───────────────────────────────────────────────────────

internal class FailingAlertService : IAlertService
{
    public Task<ServiceResult> EmitOverdueAlertAsync(Guid invoiceId, DateOnly dueDate, CancellationToken ct = default)
        => Task.FromResult<ServiceResult>(new ServiceResult.Failure("alert error"));
}

// ── SucceedingAlertService ────────────────────────────────────────────────────

internal class SucceedingAlertService : IAlertService
{
    public Task<ServiceResult> EmitOverdueAlertAsync(Guid invoiceId, DateOnly dueDate, CancellationToken ct = default)
        => Task.FromResult<ServiceResult>(new ServiceResult.Success());
}

// ── NoOpCashflowService ───────────────────────────────────────────────────────

internal class NoOpCashflowService : ICashflowProjectionService
{
    public Task<ServiceResult> InitialiseAsync(Guid invoiceId, decimal invoiceAmount, CancellationToken ct)
        => Task.FromResult<ServiceResult>(new ServiceResult.Success());

    public Task<ServiceResult> ApplyPaymentAsync(Guid invoiceId, decimal paymentAmount, decimal runningTotal, CancellationToken ct = default)
        => Task.FromResult<ServiceResult>(new ServiceResult.Success());

    public Task<ServiceResult> MarkSettledAsync(Guid invoiceId, CancellationToken ct = default)
        => Task.FromResult<ServiceResult>(new ServiceResult.Success());

    public Task<ServiceResult> MarkOverdueAsync(Guid invoiceId, CancellationToken ct)
        => Task.FromResult<ServiceResult>(new ServiceResult.Success());
}

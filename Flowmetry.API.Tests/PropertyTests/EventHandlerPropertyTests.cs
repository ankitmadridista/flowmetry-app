// Feature: domain-event-handlers, Property 4: InvoiceCreated handler schedules Initial reminder at correct offset
// Feature: domain-event-handlers, Property 5: InvoiceSent handler schedules reminders via ISender
// Feature: domain-event-handlers, Property 6: PaymentReceived handler applies payment to cashflow projection
// Feature: domain-event-handlers, Property 7: PaymentReceived handler marks projection settled when fully paid
// Feature: domain-event-handlers, Property 8: InvoiceOverdue handler emits alert and schedules escalation

using CsCheck;
using Flowmetry.Application.Common;
using Flowmetry.Application.Invoices;
using Flowmetry.Application.Invoices.Dtos;
using Flowmetry.Application.Invoices.EventHandlers;
using Flowmetry.Application.Invoices.Services;
using Flowmetry.Application.Reminders;
using Flowmetry.Application.Reminders.Commands;
using Flowmetry.Domain;
using Flowmetry.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ServicesReminderType = Flowmetry.Application.Invoices.Services.ReminderType;
using DomainReminderType = Flowmetry.Domain.ReminderType;

namespace Flowmetry.API.Tests.PropertyTests;

/// <summary>
/// Property-based tests for domain event handlers.
/// Property 4: InvoiceCreated handler schedules Initial reminder at correct offset
/// </summary>
public class EventHandlerPropertyTests
{
    // ── Property 4: InvoiceCreated schedules Initial reminder at correct offset ──
    // Validates: Requirements 2.1, 2.2

    [Fact]
    public async Task Property4_InvoiceCreated_SchedulesInitialReminderAtCorrectOffset()
    {
        // **Validates: Requirements 2.1, 2.2**
        //
        // For any InvoiceCreated event with any DueDate and any configured
        // InitialReminderDaysBeforeDue offset, the handler should call
        // IReminderScheduler.ScheduleAsync with ReminderType.Initial and a
        // ScheduledAt equal to DueDate minus the configured offset (UTC midnight).

        var gen =
            from year in Gen.Int[2020, 2040]
            from month in Gen.Int[1, 12]
            from day in Gen.Int[1, 28]
            from offset in Gen.Int[1, 30]
            select (DueDate: new DateOnly(year, month, day), Offset: offset);

        await gen.SampleAsync(async tuple =>
        {
            var (dueDate, offset) = tuple;

            var spy = new SpyReminderScheduler();
            var options = Options.Create(new ReminderOptions { InitialReminderDaysBeforeDue = offset });

            var evt = new InvoiceCreated(Guid.NewGuid(), Guid.NewGuid(), 100m, dueDate);
            var notification = new DomainEventNotification<InvoiceCreated>(evt);

            var handler = new InvoiceCreatedHandler(
                spy,
                options,
                new SpyCashflowProjectionService(),
                NullLogger<InvoiceCreatedHandler>.Instance);

            await handler.Handle(notification, CancellationToken.None);

            // Assert: exactly one ScheduleAsync call was made
            Assert.Single(spy.ScheduleCalls);

            var call = spy.ScheduleCalls[0];

            // Assert: correct InvoiceId
            Assert.Equal(evt.InvoiceId, call.InvoiceId);

            // Assert: ReminderType.Initial was passed
            Assert.Equal(ServicesReminderType.Initial, call.Type);

            // Assert: ScheduledAt == DueDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) - TimeSpan.FromDays(offset)
            var expectedScheduledAt = dueDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
                - TimeSpan.FromDays(offset);
            Assert.Equal(expectedScheduledAt, call.ScheduledAt);
        });
    }

    // ── Property 5: InvoiceSent schedules reminders via ISender ──
    // Validates: Requirements 3.1, 3.2

    [Fact]
    public async Task Property5_InvoiceSent_SchedulesRemindersViaSender()
    {
        // **Validates: Requirements 3.1, 3.2**
        //
        // For any InvoiceSent event where the referenced invoice exists in the
        // repository with any DueDate far enough in the future, the handler should
        // dispatch ScheduleReminderCommand via ISender for PostDue and Escalation
        // (and PreDue when DueDate - 3 days is in the future).

        var gen =
            from year in Gen.Int[2030, 2040]
            from month in Gen.Int[1, 12]
            from day in Gen.Int[1, 28]
            select new DateOnly(year, month, day);

        await gen.SampleAsync(async dueDate =>
        {
            var invoice = Invoice.Create(Guid.NewGuid(), 500m, dueDate);
            var repo = new StubInvoiceRepository(invoice);
            var sender = new SpySender();

            var evt = new InvoiceSent(invoice.Id, DateTimeOffset.UtcNow);
            var notification = new DomainEventNotification<InvoiceSent>(evt);

            var handler = new InvoiceSentHandler(
                repo,
                sender,
                NullLogger<InvoiceSentHandler>.Instance);

            await handler.Handle(notification, CancellationToken.None);

            // Assert: at least PostDue and Escalation commands were sent
            var commands = sender.SentRequests.OfType<ScheduleReminderCommand>().ToList();
            Assert.Contains(commands, c => c.ReminderType == DomainReminderType.PostDue);
            Assert.Contains(commands, c => c.ReminderType == DomainReminderType.Escalation);

            // Assert: all commands reference the correct invoice
            Assert.All(commands, c => Assert.Equal(invoice.Id, c.InvoiceId));
        });
    }
    // ── Property 6: PaymentReceived applies payment to cashflow projection ──
    // Validates: Requirements 4.1

    [Fact]
    public async Task Property6_PaymentReceived_AppliesPaymentToCashflowProjection()
    {
        // **Validates: Requirements 4.1**
        //
        // For any PaymentReceived event with any InvoiceId, PaymentAmount, and
        // RunningTotal (where RunningTotal < invoice Amount to avoid the settled
        // path), the handler should call ICashflowProjectionService.ApplyPaymentAsync
        // with the exact InvoiceId, PaymentAmount, and RunningTotal from the event.

        var gen =
            from invoiceId in Gen.Guid
            from amount in Gen.Decimal[1m, 10_000m]
            from runningTotal in Gen.Decimal[0.01m, amount - 0.01m]
            from paymentAmount in Gen.Decimal[0.01m, runningTotal]
            select (InvoiceId: invoiceId, Amount: amount, RunningTotal: runningTotal, PaymentAmount: paymentAmount);

        await gen.SampleAsync(async tuple =>
        {
            var (invoiceId, amount, runningTotal, paymentAmount) = tuple;

            var spy = new SpyCashflowProjectionService();

            // Use an invoice whose Amount > RunningTotal so the settled path is not triggered
            var invoice = Invoice.Create(Guid.NewGuid(), amount, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));
            var repo = new StubInvoiceRepository(invoice);

            var evt = new PaymentReceived(invoiceId, paymentAmount, runningTotal);
            var notification = new DomainEventNotification<PaymentReceived>(evt);

            var handler = new PaymentReceivedHandler(
                repo,
                spy,
                new NoOpReminderRepo(),
                NullLogger<PaymentReceivedHandler>.Instance);

            await handler.Handle(notification, CancellationToken.None);

            // Assert: exactly one ApplyPaymentAsync call was made
            Assert.Single(spy.ApplyCalls);

            var call = spy.ApplyCalls[0];

            // Assert: correct InvoiceId
            Assert.Equal(invoiceId, call.InvoiceId);

            // Assert: correct PaymentAmount
            Assert.Equal(paymentAmount, call.PaymentAmount);

            // Assert: correct RunningTotal
            Assert.Equal(runningTotal, call.RunningTotal);
        });
    }

    // ── Property 8: InvoiceOverdue emits alert and schedules escalation ──
    // Validates: Requirements 5.1, 5.2

    [Fact]
    public async Task Property8_InvoiceOverdue_EmitsAlertAndSchedulesEscalation()
    {
        // **Validates: Requirements 5.1, 5.2**
        //
        // For any InvoiceOverdue event, the handler should call both
        // IAlertService.EmitOverdueAlertAsync (with the correct InvoiceId and DueDate)
        // and IReminderScheduler.CancelOrReplaceAsync (with ReminderType.Escalation
        // and ScheduledAt == DueDate + EscalationReminderDaysAfterDue).

        var gen =
            from invoiceId in Gen.Guid
            from year in Gen.Int[2020, 2040]
            from month in Gen.Int[1, 12]
            from day in Gen.Int[1, 28]
            from escalationDays in Gen.Int[1, 30]
            select (InvoiceId: invoiceId, DueDate: new DateOnly(year, month, day), EscalationDays: escalationDays);

        await gen.SampleAsync(async tuple =>
        {
            var (invoiceId, dueDate, escalationDays) = tuple;

            var spyAlert = new SpyAlertService();
            var spyScheduler = new SpyReminderScheduler();
            var options = Options.Create(new ReminderOptions { EscalationReminderDaysAfterDue = escalationDays });

            var evt = new InvoiceOverdue(invoiceId, dueDate);
            var notification = new DomainEventNotification<InvoiceOverdue>(evt);

            var handler = new InvoiceOverdueHandler(
                spyAlert,
                spyScheduler,
                options,
                new SpyCashflowProjectionService(),
                NullLogger<InvoiceOverdueHandler>.Instance);

            await handler.Handle(notification, CancellationToken.None);

            // Assert: EmitOverdueAlertAsync was called once with correct InvoiceId and DueDate
            Assert.Single(spyAlert.AlertCalls);
            Assert.Equal(invoiceId, spyAlert.AlertCalls[0].InvoiceId);
            Assert.Equal(dueDate, spyAlert.AlertCalls[0].DueDate);

            // Assert: CancelOrReplaceAsync was called once with ReminderType.Escalation and correct ScheduledAt
            Assert.Single(spyScheduler.CancelOrReplaceCalls);
            var scheduleCall = spyScheduler.CancelOrReplaceCalls[0];
            Assert.Equal(invoiceId, scheduleCall.InvoiceId);
            Assert.Equal(ServicesReminderType.Escalation, scheduleCall.Type);

            var expectedScheduledAt = dueDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
                + TimeSpan.FromDays(escalationDays);
            Assert.Equal(expectedScheduledAt, scheduleCall.ScheduledAt);
        });
    }

    // ── Property 7: PaymentReceived marks settled when fully paid ──
    // Validates: Requirements 4.2

    [Fact]
    public async Task Property7_PaymentReceived_MarksSettledWhenFullyPaid()
    {
        // **Validates: Requirements 4.2**
        //
        // For any PaymentReceived event where RunningTotal equals the invoice Amount,
        // the handler should call ICashflowProjectionService.MarkSettledAsync in
        // addition to ApplyPaymentAsync.

        // Feature: domain-event-handlers, Property 7: PaymentReceived handler marks projection settled when fully paid

        var gen =
            from amount in Gen.Decimal[1m, 10_000m]
            select amount;

        await gen.SampleAsync(async amount =>
        {
            var spy = new SpyCashflowProjectionService();

            // Create an invoice with the generated Amount
            var invoice = Invoice.Create(Guid.NewGuid(), amount, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));

            // Stub repo returns the invoice when queried by its Id
            var repo = new StubInvoiceRepository(invoice);

            // RunningTotal == invoice.Amount → fully paid scenario
            var evt = new PaymentReceived(invoice.Id, amount, amount);
            var notification = new DomainEventNotification<PaymentReceived>(evt);

            var handler = new PaymentReceivedHandler(
                repo,
                spy,
                new NoOpReminderRepo(),
                NullLogger<PaymentReceivedHandler>.Instance);

            await handler.Handle(notification, CancellationToken.None);

            // Assert: ApplyPaymentAsync was called (always called first)
            Assert.Single(spy.ApplyCalls);
            Assert.Equal(invoice.Id, spy.ApplyCalls[0].InvoiceId);

            // Assert: MarkSettledAsync was called exactly once with the correct InvoiceId
            Assert.Single(spy.SettleCalls);
            Assert.Equal(invoice.Id, spy.SettleCalls[0].InvoiceId);
        });
    }
}

// ── SpyReminderScheduler ──────────────────────────────────────────────────────

internal class SpyReminderScheduler : IReminderScheduler
{
    public record ScheduleCall(Guid InvoiceId, ServicesReminderType Type, DateTimeOffset ScheduledAt);

    public List<ScheduleCall> ScheduleCalls { get; } = new();
    public List<ScheduleCall> CancelOrReplaceCalls { get; } = new();

    public Task<ServiceResult> ScheduleAsync(Guid invoiceId, ServicesReminderType type, DateTimeOffset scheduledAt, CancellationToken ct = default)
    {
        ScheduleCalls.Add(new(invoiceId, type, scheduledAt));
        return Task.FromResult<ServiceResult>(new ServiceResult.Success());
    }

    public Task<ServiceResult> CancelOrReplaceAsync(Guid invoiceId, ServicesReminderType type, DateTimeOffset scheduledAt, CancellationToken ct = default)
    {
        CancelOrReplaceCalls.Add(new(invoiceId, type, scheduledAt));
        return Task.FromResult<ServiceResult>(new ServiceResult.Success());
    }
}

// ── StubInvoiceRepository ─────────────────────────────────────────────────────

internal class StubInvoiceRepository : IInvoiceRepository
{
    private readonly Invoice? _invoice;
    public StubInvoiceRepository(Invoice? invoice) => _invoice = invoice;

    public Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_invoice);

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

    public Task<PagedResult<Invoice>> GetPagedAsync(InvoiceFilter filter, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<PagedResult<InvoiceSummaryDto>> GetPagedSummariesAsync(InvoiceFilter filter, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<InvoiceDetailsDto?> GetDetailsByIdAsync(Guid id, CancellationToken ct = default)
        => throw new NotImplementedException();
}

// ── SpyCashflowProjectionService ─────────────────────────────────────────────

internal class SpyCashflowProjectionService : ICashflowProjectionService
{
    public record ApplyCall(Guid InvoiceId, decimal PaymentAmount, decimal RunningTotal);
    public record SettleCall(Guid InvoiceId);
    public record InitialiseCall(Guid InvoiceId, decimal InvoiceAmount);
    public record MarkOverdueCall(Guid InvoiceId);

    public List<ApplyCall> ApplyCalls { get; } = new();
    public List<SettleCall> SettleCalls { get; } = new();
    public List<InitialiseCall> InitialiseCalls { get; } = new();
    public List<MarkOverdueCall> MarkOverdueCalls { get; } = new();

    public Task<ServiceResult> InitialiseAsync(Guid invoiceId, decimal invoiceAmount, CancellationToken ct)
    {
        InitialiseCalls.Add(new(invoiceId, invoiceAmount));
        return Task.FromResult<ServiceResult>(new ServiceResult.Success());
    }

    public Task<ServiceResult> ApplyPaymentAsync(Guid invoiceId, decimal paymentAmount, decimal runningTotal, CancellationToken ct)
    {
        ApplyCalls.Add(new(invoiceId, paymentAmount, runningTotal));
        return Task.FromResult<ServiceResult>(new ServiceResult.Success());
    }

    public Task<ServiceResult> MarkSettledAsync(Guid invoiceId, CancellationToken ct)
    {
        SettleCalls.Add(new(invoiceId));
        return Task.FromResult<ServiceResult>(new ServiceResult.Success());
    }

    public Task<ServiceResult> MarkOverdueAsync(Guid invoiceId, CancellationToken ct)
    {
        MarkOverdueCalls.Add(new(invoiceId));
        return Task.FromResult<ServiceResult>(new ServiceResult.Success());
    }
}

// ── SpyAlertService ───────────────────────────────────────────────────────────

internal class SpyAlertService : IAlertService
{
    public record AlertCall(Guid InvoiceId, DateOnly DueDate);
    public List<AlertCall> AlertCalls { get; } = new();

    public Task<ServiceResult> EmitOverdueAlertAsync(Guid invoiceId, DateOnly dueDate, CancellationToken ct)
    {
        AlertCalls.Add(new(invoiceId, dueDate));
        return Task.FromResult<ServiceResult>(new ServiceResult.Success());
    }
}

// ── SpySender ─────────────────────────────────────────────────────────────────

internal class SpySender : ISender
{
    public List<object> SentRequests { get; } = new();

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        SentRequests.Add(request);
        return Task.FromResult(default(TResponse)!);
    }

    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
    {
        SentRequests.Add(request!);
        return Task.CompletedTask;
    }

    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        SentRequests.Add(request);
        return Task.FromResult<object?>(null);
    }

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}

// ── NoOpReminderRepo ──────────────────────────────────────────────────────────

internal class NoOpReminderRepo : IReminderRepository
{
    public Task AddAsync(Reminder reminder, CancellationToken ct = default) => Task.CompletedTask;

    public Task<IReadOnlyList<Reminder>> GetPendingDueAsync(DateTimeOffset utcNow, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Reminder>>(new List<Reminder>());

    public Task<IReadOnlyList<Reminder>> GetByInvoiceIdAsync(Guid invoiceId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Reminder>>(new List<Reminder>());

    public Task<IReadOnlyList<Reminder>> GetPendingByInvoiceIdAsync(Guid invoiceId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Reminder>>(new List<Reminder>());

    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
}

// Feature: cashflow-dashboard, Property 10: InvoiceCreatedHandler calls InitialiseAsync exactly once with the correct InvoiceId and Amount for any InvoiceCreated event
// Feature: cashflow-dashboard, Property 11: InvoiceOverdueHandler calls MarkOverdueAsync exactly once with the correct InvoiceId for any InvoiceOverdue event

using CsCheck;
using Flowmetry.Application.Common;
using Flowmetry.Application.Invoices;
using Flowmetry.Application.Invoices.EventHandlers;
using Flowmetry.Application.Invoices.Services;
using Flowmetry.Domain.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Flowmetry.API.Tests.PropertyTests;

/// <summary>
/// Property-based tests for cashflow-related event handler wiring.
/// Property 10: InvoiceCreatedHandler calls InitialiseAsync with correct arguments.
/// Property 11: InvoiceOverdueHandler calls MarkOverdueAsync with correct InvoiceId.
/// </summary>
public class CashflowHandlerPropertyTests
{
    // ── Property 10: InvoiceCreatedHandler calls InitialiseAsync exactly once ──
    // Validates: Requirements 2.4

    [Fact]
    public async Task Property10_InvoiceCreatedHandler_CallsInitialiseAsyncOnce()
    {
        // **Validates: Requirements 2.4**
        //
        // For any InvoiceCreated event with any InvoiceId and Amount, the
        // InvoiceCreatedHandler should call ICashflowProjectionService.InitialiseAsync
        // exactly once with the matching InvoiceId and Amount.

        var gen =
            from invoiceId in Gen.Guid
            from customerId in Gen.Guid
            from amount in Gen.Int[1, 1_000_000].Select(i => (decimal)i / 100m)
            from year in Gen.Int[2020, 2040]
            from month in Gen.Int[1, 12]
            from day in Gen.Int[1, 28]
            select (InvoiceId: invoiceId, CustomerId: customerId, Amount: amount,
                    DueDate: new DateOnly(year, month, day));

        await gen.SampleAsync(async tuple =>
        {
            var (invoiceId, customerId, amount, dueDate) = tuple;

            var spy = new TrackingCashflowProjectionService();
            var spyScheduler = new StubReminderScheduler();
            var options = Options.Create(new ReminderOptions { InitialReminderDaysBeforeDue = 7 });

            var evt = new InvoiceCreated(invoiceId, customerId, amount, dueDate);
            var notification = new DomainEventNotification<InvoiceCreated>(evt);

            var handler = new InvoiceCreatedHandler(
                spyScheduler,
                options,
                spy,
                NullLogger<InvoiceCreatedHandler>.Instance);

            await handler.Handle(notification, CancellationToken.None);

            // Assert: InitialiseAsync called exactly once
            Assert.Single(spy.InitialiseCalls);

            var call = spy.InitialiseCalls[0];

            // Assert: correct InvoiceId
            Assert.Equal(invoiceId, call.InvoiceId);

            // Assert: correct Amount
            Assert.Equal(amount, call.InvoiceAmount);
        });
    }

    // ── Property 11: InvoiceOverdueHandler calls MarkOverdueAsync exactly once ──
    // Validates: Requirements 5.4

    [Fact]
    public async Task Property11_InvoiceOverdueHandler_CallsMarkOverdueAsyncOnce()
    {
        // **Validates: Requirements 5.4**
        //
        // For any InvoiceOverdue event with any InvoiceId, the InvoiceOverdueHandler
        // should call ICashflowProjectionService.MarkOverdueAsync exactly once with
        // the matching InvoiceId.

        var gen =
            from invoiceId in Gen.Guid
            from year in Gen.Int[2020, 2040]
            from month in Gen.Int[1, 12]
            from day in Gen.Int[1, 28]
            select (InvoiceId: invoiceId, DueDate: new DateOnly(year, month, day));

        await gen.SampleAsync(async tuple =>
        {
            var (invoiceId, dueDate) = tuple;

            var spy = new TrackingCashflowProjectionService();
            var spyAlert = new StubAlertService();
            var spyScheduler = new StubReminderScheduler();
            var options = Options.Create(new ReminderOptions { EscalationReminderDaysAfterDue = 7 });

            var evt = new InvoiceOverdue(invoiceId, dueDate);
            var notification = new DomainEventNotification<InvoiceOverdue>(evt);

            var handler = new InvoiceOverdueHandler(
                spyAlert,
                spyScheduler,
                options,
                spy,
                NullLogger<InvoiceOverdueHandler>.Instance);

            await handler.Handle(notification, CancellationToken.None);

            // Assert: MarkOverdueAsync called exactly once
            Assert.Single(spy.MarkOverdueCalls);

            // Assert: correct InvoiceId
            Assert.Equal(invoiceId, spy.MarkOverdueCalls[0]);
        });
    }
}

// ── TrackingCashflowProjectionService ─────────────────────────────────────────
// Spy that records calls to InitialiseAsync and MarkOverdueAsync.

internal class TrackingCashflowProjectionService : ICashflowProjectionService
{
    public record InitialiseCall(Guid InvoiceId, decimal InvoiceAmount);

    public List<InitialiseCall> InitialiseCalls { get; } = new();
    public List<Guid> MarkOverdueCalls { get; } = new();

    public Task<ServiceResult> InitialiseAsync(Guid invoiceId, decimal invoiceAmount, CancellationToken ct)
    {
        InitialiseCalls.Add(new(invoiceId, invoiceAmount));
        return Task.FromResult<ServiceResult>(new ServiceResult.Success());
    }

    public Task<ServiceResult> ApplyPaymentAsync(Guid invoiceId, decimal paymentAmount, decimal runningTotal, CancellationToken ct)
        => Task.FromResult<ServiceResult>(new ServiceResult.Success());

    public Task<ServiceResult> MarkSettledAsync(Guid invoiceId, CancellationToken ct)
        => Task.FromResult<ServiceResult>(new ServiceResult.Success());

    public Task<ServiceResult> MarkOverdueAsync(Guid invoiceId, CancellationToken ct)
    {
        MarkOverdueCalls.Add(invoiceId);
        return Task.FromResult<ServiceResult>(new ServiceResult.Success());
    }
}

// ── StubReminderScheduler ─────────────────────────────────────────────────────

internal class StubReminderScheduler : IReminderScheduler
{
    public Task<ServiceResult> ScheduleAsync(Guid invoiceId, ReminderType type, DateTimeOffset scheduledAt, CancellationToken ct = default)
        => Task.FromResult<ServiceResult>(new ServiceResult.Success());

    public Task<ServiceResult> CancelOrReplaceAsync(Guid invoiceId, ReminderType type, DateTimeOffset scheduledAt, CancellationToken ct = default)
        => Task.FromResult<ServiceResult>(new ServiceResult.Success());
}

// ── StubAlertService ──────────────────────────────────────────────────────────

internal class StubAlertService : IAlertService
{
    public Task<ServiceResult> EmitOverdueAlertAsync(Guid invoiceId, DateOnly dueDate, CancellationToken ct)
        => Task.FromResult<ServiceResult>(new ServiceResult.Success());
}

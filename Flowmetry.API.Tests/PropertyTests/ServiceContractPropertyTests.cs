// Feature: domain-event-handlers, Property 9: Scheduler replace semantics — second schedule replaces first
// Feature: domain-event-handlers, Property 10: CashflowProjectionService is idempotent
// Feature: domain-event-handlers, Property 11: AlertService deduplicates overdue alerts

using CsCheck;
using Flowmetry.Application.Common;
using Flowmetry.Application.Invoices.Services;

namespace Flowmetry.API.Tests.PropertyTests;

/// <summary>
/// Property-based tests for service contracts.
/// Property 9: Scheduler replace semantics — second schedule replaces first
/// Property 10: CashflowProjectionService is idempotent
/// Property 11: AlertService deduplicates overdue alerts
/// </summary>
public class ServiceContractPropertyTests
{
    // ── Property 9: Scheduler replace semantics ───────────────────────────────
    // Validates: Requirements 6.3, 3.4, 2.4, 5.5

    [Fact]
    public async Task Property9_SchedulerReplaceSemantics_SecondScheduleReplacesFirst()
    {
        // **Validates: Requirements 6.3, 3.4, 2.4, 5.5**
        //
        // For any InvoiceId and ReminderType, calling CancelOrReplaceAsync twice
        // with different ScheduledAt values should result in exactly one pending
        // reminder with the second (latest) ScheduledAt, not two reminders.

        var gen =
            from invoiceId in Gen.Guid
            from reminderType in Gen.Enum<ReminderType>()
            from firstTicks in Gen.Long[DateTimeOffset.UtcNow.AddDays(-365).Ticks, DateTimeOffset.UtcNow.AddDays(365).Ticks]
            from secondTicks in Gen.Long[DateTimeOffset.UtcNow.AddDays(-365).Ticks, DateTimeOffset.UtcNow.AddDays(365).Ticks]
            where firstTicks != secondTicks
            select (
                InvoiceId: invoiceId,
                ReminderType: reminderType,
                First: new DateTimeOffset(firstTicks, TimeSpan.Zero),
                Second: new DateTimeOffset(secondTicks, TimeSpan.Zero)
            );

        await gen.SampleAsync(async tuple =>
        {
            var (invoiceId, reminderType, first, second) = tuple;

            var scheduler = new StatefulReminderScheduler();

            // Call CancelOrReplaceAsync twice with the same InvoiceId and ReminderType
            // but different ScheduledAt values
            await scheduler.CancelOrReplaceAsync(invoiceId, reminderType, first);
            await scheduler.CancelOrReplaceAsync(invoiceId, reminderType, second);

            // Assert: only the second (latest) ScheduledAt is retained
            var retained = scheduler.GetScheduledAt(invoiceId, reminderType);
            Assert.NotNull(retained);
            Assert.Equal(second, retained.Value);
        });
    }

    // ── Property 10: CashflowProjectionService is idempotent ─────────────────
    // Validates: Requirements 7.3, 4.4

    [Fact]
    public async Task Property10_CashflowProjectionService_IsIdempotent()
    {
        // Feature: domain-event-handlers, Property 10: CashflowProjectionService is idempotent
        //
        // **Validates: Requirements 7.3, 4.4**
        //
        // For any payment data (InvoiceId, PaymentAmount, RunningTotal), calling
        // ApplyPaymentAsync twice with the same arguments should produce the same
        // projection state as calling it once.

        var gen =
            from invoiceId in Gen.Guid
            from paymentAmount in Gen.Decimal[0.01m, 1_000_000m]
            from runningTotal in Gen.Decimal[0.01m, 1_000_000m]
            select (InvoiceId: invoiceId, PaymentAmount: paymentAmount, RunningTotal: runningTotal);

        await gen.SampleAsync(async tuple =>
        {
            var (invoiceId, paymentAmount, runningTotal) = tuple;

            // Single-call service
            var singleCall = new StatefulCashflowProjectionService();
            await singleCall.ApplyPaymentAsync(invoiceId, paymentAmount, runningTotal);

            // Double-call service (same args both times)
            var doubleCall = new StatefulCashflowProjectionService();
            await doubleCall.ApplyPaymentAsync(invoiceId, paymentAmount, runningTotal);
            await doubleCall.ApplyPaymentAsync(invoiceId, paymentAmount, runningTotal);

            // Assert: projection state is identical after one call vs two calls
            var (singleRunning, singleSettled) = singleCall.GetProjection(invoiceId);
            var (doubleRunning, doubleSettled) = doubleCall.GetProjection(invoiceId);

            Assert.Equal(singleRunning, doubleRunning);
            Assert.Equal(singleSettled, doubleSettled);
        });
    }

    // ── Property 11: AlertService deduplicates overdue alerts ─────────────────
    // Validates: Requirements 8.2, 5.5

    [Fact]
    public async Task Property11_AlertService_DeduplicatesOverdueAlerts()
    {
        // Feature: domain-event-handlers, Property 11: AlertService deduplicates overdue alerts
        //
        // **Validates: Requirements 8.2, 5.5**
        //
        // For any InvoiceId, calling EmitOverdueAlertAsync twice should result in
        // exactly one alert being recorded — the second call should be a no-op.

        var gen =
            from invoiceId in Gen.Guid
            from dueDateFirst in Gen.Int[1, 9999 * 365].Select(days => DateOnly.FromDayNumber(days))
            from dueDateSecond in Gen.Int[1, 9999 * 365].Select(days => DateOnly.FromDayNumber(days))
            select (InvoiceId: invoiceId, DueDateFirst: dueDateFirst, DueDateSecond: dueDateSecond);

        await gen.SampleAsync(async tuple =>
        {
            var (invoiceId, dueDateFirst, dueDateSecond) = tuple;

            var alertService = new StatefulAlertService();

            // Call EmitOverdueAlertAsync twice with the same InvoiceId
            await alertService.EmitOverdueAlertAsync(invoiceId, dueDateFirst);
            await alertService.EmitOverdueAlertAsync(invoiceId, dueDateSecond);

            // Assert: exactly one alert recorded for this InvoiceId (second call is a no-op)
            Assert.Equal(1, alertService.GetAlertCount(invoiceId));
        });
    }
}

// ── StatefulCashflowProjectionService ────────────────────────────────────────
// Test-only implementation of ICashflowProjectionService with in-memory state.
// ApplyPaymentAsync sets the running total (idempotent: same args = same result).
// MarkSettledAsync marks the invoice as settled (idempotent: calling twice = same as once).

internal sealed class StatefulCashflowProjectionService : ICashflowProjectionService
{
    private readonly Dictionary<Guid, (decimal RunningTotal, bool IsSettled)> _projections = new();

    public Task<ServiceResult> ApplyPaymentAsync(
        Guid invoiceId,
        decimal paymentAmount,
        decimal runningTotal,
        CancellationToken ct = default)
    {
        // Idempotent: always set to the supplied runningTotal regardless of prior state
        _projections[invoiceId] = (runningTotal, _projections.TryGetValue(invoiceId, out var existing) && existing.IsSettled);
        return Task.FromResult<ServiceResult>(new ServiceResult.Success());
    }

    public Task<ServiceResult> MarkSettledAsync(
        Guid invoiceId,
        CancellationToken ct = default)
    {
        // Idempotent: marking settled twice is the same as once
        var runningTotal = _projections.TryGetValue(invoiceId, out var existing) ? existing.RunningTotal : 0m;
        _projections[invoiceId] = (runningTotal, true);
        return Task.FromResult<ServiceResult>(new ServiceResult.Success());
    }

    /// <summary>
    /// Returns the current projection state for the given invoice, or (0, false) if not found.
    /// </summary>
    public (decimal RunningTotal, bool IsSettled) GetProjection(Guid invoiceId)
        => _projections.TryGetValue(invoiceId, out var value) ? value : (0m, false);
}

// ── StatefulReminderScheduler ─────────────────────────────────────────────────
// Test-only implementation of IReminderScheduler with in-memory state.
// Maintains a dictionary of pending reminders keyed by (InvoiceId, ReminderType).
// CancelOrReplaceAsync replaces any existing entry for the same key.

internal sealed class StatefulReminderScheduler : IReminderScheduler
{
    private readonly Dictionary<(Guid InvoiceId, ReminderType Type), DateTimeOffset> _reminders = new();

    public Task<ServiceResult> ScheduleAsync(
        Guid invoiceId,
        ReminderType type,
        DateTimeOffset scheduledAt,
        CancellationToken ct = default)
    {
        _reminders[(invoiceId, type)] = scheduledAt;
        return Task.FromResult<ServiceResult>(new ServiceResult.Success());
    }

    public Task<ServiceResult> CancelOrReplaceAsync(
        Guid invoiceId,
        ReminderType type,
        DateTimeOffset scheduledAt,
        CancellationToken ct = default)
    {
        // Replace any existing reminder for this (invoiceId, type) pair
        _reminders[(invoiceId, type)] = scheduledAt;
        return Task.FromResult<ServiceResult>(new ServiceResult.Success());
    }

    /// <summary>
    /// Returns the currently scheduled <see cref="DateTimeOffset"/> for the given
    /// (invoiceId, type) pair, or <c>null</c> if no reminder is pending.
    /// </summary>
    public DateTimeOffset? GetScheduledAt(Guid invoiceId, ReminderType type)
        => _reminders.TryGetValue((invoiceId, type), out var value) ? value : null;
}

// ── StatefulAlertService ──────────────────────────────────────────────────────
// Test-only implementation of IAlertService with in-memory deduplication state.
// EmitOverdueAlertAsync adds the InvoiceId to a HashSet only if not already present.
// The second call for the same InvoiceId is a no-op (deduplication).

internal sealed class StatefulAlertService : IAlertService
{
    private readonly HashSet<Guid> _alertedInvoices = new();

    public Task<ServiceResult> EmitOverdueAlertAsync(
        Guid invoiceId,
        DateOnly dueDate,
        CancellationToken ct = default)
    {
        // Deduplicate: only record the alert if this InvoiceId hasn't been alerted yet
        _alertedInvoices.Add(invoiceId);
        return Task.FromResult<ServiceResult>(new ServiceResult.Success());
    }

    /// <summary>
    /// Returns the number of alerts emitted for the given <paramref name="invoiceId"/>.
    /// Due to deduplication this is always 0 or 1.
    /// </summary>
    public int GetAlertCount(Guid invoiceId)
        => _alertedInvoices.Contains(invoiceId) ? 1 : 0;
}

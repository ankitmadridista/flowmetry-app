using Flowmetry.Application.Invoices.Services;
using Flowmetry.Application.Reminders;
using Flowmetry.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Flowmetry.Infrastructure;

/// <summary>
/// Background service that polls for pending reminders and dispatches them via INotificationService.
/// Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6
/// </summary>
public sealed class RemindersEngine(
    IServiceScopeFactory scopeFactory,
    IOptions<ReminderOptions> options,
    ILogger<RemindersEngine> logger) : IHostedService
{
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(options.Value.PollingIntervalSeconds);
    private Task? _executingTask;
    private CancellationTokenSource? _cts;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _executingTask = ExecuteAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_executingTask is null)
            return;

        _cts?.Cancel();

        // Wait for the current in-flight reminder to finish before stopping (Req 4.6)
        await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));
    }

    private async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("RemindersEngine started. Polling interval: {Interval}s", _pollingInterval.TotalSeconds);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_pollingInterval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await ProcessPendingRemindersAsync(ct);
        }

        logger.LogInformation("RemindersEngine stopped.");
    }

    private async Task ProcessPendingRemindersAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var reminderRepo = scope.ServiceProvider.GetRequiredService<IReminderRepository>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<FlowmetryDbContext>();

        IReadOnlyList<Reminder> pending;
        try
        {
            pending = await reminderRepo.GetPendingDueAsync(DateTimeOffset.UtcNow, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "RemindersEngine failed to fetch pending reminders.");
            return;
        }

        if (pending.Count == 0)
            return;

        // Load into PriorityQueue ordered by ScheduledAt ascending (Req 4.3)
        var queue = new PriorityQueue<Reminder, DateTimeOffset>(pending.Count);
        foreach (var reminder in pending)
            queue.Enqueue(reminder, reminder.ScheduledAt);

        while (queue.TryDequeue(out var reminder, out _))
        {
            // Resolve customer for this reminder
            Customer? customer;
            try
            {
                customer = await dbContext.Customers.FindAsync([reminder.CustomerId], ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "RemindersEngine failed to resolve Customer {CustomerId} for Reminder {ReminderId}. Skipping.",
                    reminder.CustomerId, reminder.Id);
                continue;
            }

            if (customer is null)
            {
                logger.LogWarning(
                    "RemindersEngine: Customer {CustomerId} not found for Reminder {ReminderId}. Skipping.",
                    reminder.CustomerId, reminder.Id);
                continue;
            }

            // Dispatch notification — on failure, log and leave reminder Pending (Req 4.5)
            // Use CancellationToken.None so an in-flight reminder completes even after cancellation (Req 4.6)
            try
            {
                await notificationService.SendReminderAsync(reminder, customer, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "RemindersEngine: INotificationService failed for Reminder {ReminderId} (InvoiceId={InvoiceId}). Reminder stays Pending.",
                    reminder.Id, reminder.InvoiceId);
                continue;
            }

            reminder.MarkAsSent();
            await reminderRepo.SaveChangesAsync(CancellationToken.None);

            logger.LogInformation(
                "RemindersEngine: Reminder {ReminderId} sent (InvoiceId={InvoiceId}, Type={ReminderType}).",
                reminder.Id, reminder.InvoiceId, reminder.ReminderType);
        }
    }
}

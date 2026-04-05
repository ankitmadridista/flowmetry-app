using Flowmetry.Application.Reminders;
using Flowmetry.Domain;
using Microsoft.Extensions.Logging;

namespace Flowmetry.Infrastructure.Events.Stubs;

public class LoggingNotificationService(ILogger<LoggingNotificationService> logger) : INotificationService
{
    public Task SendReminderAsync(Reminder reminder, Customer customer, CancellationToken ct = default)
    {
        logger.LogInformation(
            "SendReminderAsync called: InvoiceId={InvoiceId}, ReminderType={ReminderType}, ScheduledAt={ScheduledAt}, CustomerId={CustomerId}, CustomerName={CustomerName}, CustomerEmail={CustomerEmail}",
            reminder.InvoiceId,
            reminder.ReminderType,
            reminder.ScheduledAt,
            customer.Id,
            customer.Name,
            customer.Email);

        return Task.CompletedTask;
    }
}

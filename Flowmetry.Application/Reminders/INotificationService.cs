using Flowmetry.Domain;

namespace Flowmetry.Application.Reminders;

public interface INotificationService
{
    Task SendReminderAsync(Reminder reminder, Customer customer, CancellationToken ct = default);
}

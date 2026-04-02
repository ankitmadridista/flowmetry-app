namespace Flowmetry.Application.Invoices.Services;

public class ReminderOptions
{
    public const string SectionName = "Reminders";
    public int InitialReminderDaysBeforeDue { get; init; } = 7;
    public int DueDateReminderDaysBeforeDue { get; init; } = 3;
    public int EscalationReminderDaysAfterDue { get; init; } = 1;
}

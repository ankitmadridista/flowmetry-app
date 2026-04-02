using Flowmetry.Application.Invoices.Services;
using Microsoft.Extensions.Configuration;

namespace Flowmetry.API.Tests.UnitTests;

public class ReminderOptionsBindingTests
{
    [Fact]
    public void ReminderOptions_BindsCorrectlyFromConfiguration()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Reminders:InitialReminderDaysBeforeDue"] = "14",
                ["Reminders:DueDateReminderDaysBeforeDue"] = "5",
                ["Reminders:EscalationReminderDaysAfterDue"] = "3"
            })
            .Build();

        // Act — use Get<T>() which supports init setters
        var options = config.GetSection(ReminderOptions.SectionName).Get<ReminderOptions>()!;

        // Assert
        Assert.Equal(14, options.InitialReminderDaysBeforeDue);
        Assert.Equal(5, options.DueDateReminderDaysBeforeDue);
        Assert.Equal(3, options.EscalationReminderDaysAfterDue);
    }

    [Fact]
    public void ReminderOptions_UsesDefaultValuesWhenNotConfigured()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act — empty section returns null, so fall back to a fresh instance with defaults
        var options = config.GetSection(ReminderOptions.SectionName).Get<ReminderOptions>()
                      ?? new ReminderOptions();

        // Assert: defaults are 7, 3, 1
        Assert.Equal(7, options.InitialReminderDaysBeforeDue);
        Assert.Equal(3, options.DueDateReminderDaysBeforeDue);
        Assert.Equal(1, options.EscalationReminderDaysAfterDue);
    }
}

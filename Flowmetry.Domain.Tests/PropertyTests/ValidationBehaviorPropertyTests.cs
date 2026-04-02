using CsCheck;
using Flowmetry.Application.Invoices.Commands;
using Flowmetry.Application.Invoices.Validators;

namespace Flowmetry.Domain.Tests.PropertyTests;

public class ValidationBehaviorPropertyTests
{
    [Fact]
    public async Task Property12_ValidationBehavior_RejectsInvalidCreateInvoiceCommand()
    {
        // Feature: invoice-domain, Property 12: ValidationBehavior rejects invalid CreateInvoiceCommand inputs
        var validator = new CreateInvoiceCommandValidator();

        // Test: Amount <= 0 should fail
        // Generate non-positive amounts by negating a positive value
        await Gen.Decimal[0.01m, 1000m]
           .Select(v => -v)
           .SampleAsync(async amount =>
           {
               var command = new CreateInvoiceCommand(Guid.NewGuid(), amount, DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
               var result = await validator.ValidateAsync(command);
               Assert.False(result.IsValid);
           });

        // Test: DueDate in past should fail
        await Gen.Int[1, 365]
           .SampleAsync(async daysAgo =>
           {
               var command = new CreateInvoiceCommand(Guid.NewGuid(), 100m, DateOnly.FromDateTime(DateTime.Today.AddDays(-daysAgo)));
               var result = await validator.ValidateAsync(command);
               Assert.False(result.IsValid);
           });
    }
}

using FluentValidation;
using Flowmetry.Application.Invoices.Commands;

namespace Flowmetry.Application.Invoices.Validators;

public class CreateInvoiceCommandValidator : AbstractValidator<CreateInvoiceCommand>
{
    public CreateInvoiceCommandValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Amount must be greater than zero.");
        RuleFor(x => x.DueDate).GreaterThan(DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage("DueDate must be in the future.");
    }
}

using FluentValidation;
using Flowmetry.Application.Invoices.Commands;

namespace Flowmetry.Application.Invoices.Validators;

public class RecordPaymentCommandValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentCommandValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Amount must be greater than zero.");
    }
}

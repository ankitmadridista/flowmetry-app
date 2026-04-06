using FluentValidation;
using Flowmetry.Application.Invoices.Queries;

namespace Flowmetry.Application.Invoices.Validators;

public class GetInvoicesQueryValidator : AbstractValidator<GetInvoicesQuery>
{
    public GetInvoicesQueryValidator()
    {
        RuleFor(x => x.Filter.Page)
            .GreaterThanOrEqualTo(0).WithMessage("page must be >= 0.");

        RuleFor(x => x.Filter.PageSize)
            .InclusiveBetween(1, 100).WithMessage("pageSize must be between 1 and 100.");

        RuleFor(x => x.Filter)
            .Must(f => f.DueDateFrom is null || f.DueDateTo is null || f.DueDateFrom <= f.DueDateTo)
            .WithMessage("dueDateFrom must not be later than dueDateTo.");
    }
}

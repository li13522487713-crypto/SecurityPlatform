using FluentValidation;
using Atlas.Core.Models;

namespace Atlas.Application.Validators;

public sealed class PagedRequestValidator : AbstractValidator<PagedRequest>
{
    public PagedRequestValidator()
    {
        RuleFor(x => x.PageIndex).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Keyword).MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.Keyword));
    }
}
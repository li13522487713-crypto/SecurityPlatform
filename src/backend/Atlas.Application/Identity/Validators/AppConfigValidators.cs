using Atlas.Application.Identity.Models;
using FluentValidation;

namespace Atlas.Application.Identity.Validators;

public sealed class AppConfigUpdateRequestValidator : AbstractValidator<AppConfigUpdateRequest>
{
    public AppConfigUpdateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Description)
            .MaximumLength(200);

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0);
    }
}

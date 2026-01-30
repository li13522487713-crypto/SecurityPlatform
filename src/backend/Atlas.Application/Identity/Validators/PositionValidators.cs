using FluentValidation;
using Atlas.Application.Identity.Models;

namespace Atlas.Application.Identity.Validators;

public sealed class PositionCreateRequestValidator : AbstractValidator<PositionCreateRequest>
{
    public PositionCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Description).MaximumLength(256).When(x => x.Description is not null);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class PositionUpdateRequestValidator : AbstractValidator<PositionUpdateRequest>
{
    public PositionUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Description).MaximumLength(256).When(x => x.Description is not null);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

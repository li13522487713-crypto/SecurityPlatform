using FluentValidation;
using Atlas.Application.Identity.Models;

namespace Atlas.Application.Identity.Validators;

public sealed class DepartmentCreateRequestValidator : AbstractValidator<DepartmentCreateRequest>
{
    public DepartmentCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class DepartmentUpdateRequestValidator : AbstractValidator<DepartmentUpdateRequest>
{
    public DepartmentUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

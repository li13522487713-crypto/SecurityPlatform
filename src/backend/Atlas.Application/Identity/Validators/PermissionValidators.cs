using FluentValidation;
using Atlas.Application.Identity.Models;
using Atlas.Application.Identity;

namespace Atlas.Application.Identity.Validators;

public sealed class PermissionCreateRequestValidator : AbstractValidator<PermissionCreateRequest>
{
    public PermissionCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Type).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Type)
            .Must(PermissionTypes.IsSupported)
            .WithMessage("权限类型必须为 Api/Menu/Application/Page/Action 之一");
        RuleFor(x => x.Description).MaximumLength(256).When(x => x.Description is not null);
    }
}

public sealed class PermissionUpdateRequestValidator : AbstractValidator<PermissionUpdateRequest>
{
    public PermissionUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Type).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Type)
            .Must(PermissionTypes.IsSupported)
            .WithMessage("权限类型必须为 Api/Menu/Application/Page/Action 之一");
        RuleFor(x => x.Description).MaximumLength(256).When(x => x.Description is not null);
    }
}

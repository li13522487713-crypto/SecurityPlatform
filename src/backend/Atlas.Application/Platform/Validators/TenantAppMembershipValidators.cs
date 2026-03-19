using Atlas.Application.Platform.Models;
using FluentValidation;

namespace Atlas.Application.Platform.Validators;

public sealed class TenantAppMemberAssignRequestValidator : AbstractValidator<TenantAppMemberAssignRequest>
{
    public TenantAppMemberAssignRequestValidator()
    {
        RuleFor(x => x.UserIds)
            .NotNull()
            .Must(x => x.Count > 0)
            .WithMessage("至少选择一名应用成员。");

        RuleForEach(x => x.UserIds)
            .GreaterThan(0);

        RuleFor(x => x.RoleIds)
            .NotNull();

        RuleForEach(x => x.RoleIds)
            .GreaterThan(0);
    }
}

public sealed class TenantAppMemberUpdateRolesRequestValidator : AbstractValidator<TenantAppMemberUpdateRolesRequest>
{
    public TenantAppMemberUpdateRolesRequestValidator()
    {
        RuleFor(x => x.RoleIds)
            .NotNull();

        RuleForEach(x => x.RoleIds)
            .GreaterThan(0);
    }
}

public sealed class TenantAppRoleCreateRequestValidator : AbstractValidator<TenantAppRoleCreateRequest>
{
    public TenantAppRoleCreateRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(64)
            .Matches(@"^[A-Za-z][A-Za-z0-9:_-]*$")
            .WithMessage("角色编码仅支持字母开头，后续使用字母数字冒号下划线或中划线。");

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(x => x.Description)
            .MaximumLength(256)
            .When(x => x.Description is not null);

        RuleFor(x => x.PermissionCodes)
            .NotNull();

        RuleForEach(x => x.PermissionCodes)
            .NotEmpty()
            .MaximumLength(128);
    }
}

public sealed class TenantAppRoleUpdateRequestValidator : AbstractValidator<TenantAppRoleUpdateRequest>
{
    public TenantAppRoleUpdateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(x => x.Description)
            .MaximumLength(256)
            .When(x => x.Description is not null);
    }
}

public sealed class TenantAppRoleAssignPermissionsRequestValidator : AbstractValidator<TenantAppRoleAssignPermissionsRequest>
{
    public TenantAppRoleAssignPermissionsRequestValidator()
    {
        RuleFor(x => x.PermissionCodes)
            .NotNull();

        RuleForEach(x => x.PermissionCodes)
            .NotEmpty()
            .MaximumLength(128);
    }
}

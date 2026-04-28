using Atlas.Application.Platform.Models;
using Atlas.Application.Resources;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace Atlas.Application.Platform.Validators;

public sealed class TenantAppMemberAssignRequestValidator : AbstractValidator<TenantAppMemberAssignRequest>
{
    public TenantAppMemberAssignRequestValidator(IStringLocalizer<Messages> localizer)
    {
        RuleFor(x => x.UserIds)
            .NotNull()
            .Must(x => x.Count > 0)
            .WithMessage(localizer["AppMemberRequired"].Value);

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
    public TenantAppRoleCreateRequestValidator(IStringLocalizer<Messages> localizer)
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(64)
            .Matches(@"^[A-Za-z][A-Za-z0-9:_-]*$")
            .WithMessage(localizer["AppRoleCodeFormat"].Value);

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

public sealed class TenantAppFileStorageSettingsUpdateRequestValidator : AbstractValidator<TenantAppFileStorageSettingsUpdateRequest>
{
    public TenantAppFileStorageSettingsUpdateRequestValidator()
    {
        RuleFor(x => x.OverrideBasePath)
            .MaximumLength(200)
            .When(x => x.OverrideBasePath is not null);

        RuleFor(x => x.OverrideMinioBucketName)
            .MaximumLength(128)
            .When(x => x.OverrideMinioBucketName is not null);

        RuleFor(x => x.OverrideBasePath)
            .NotEmpty()
            .When(x => !x.InheritBasePath)
            .WithMessage("禁用继承时必须提供应用级 BasePath。");

        RuleFor(x => x.OverrideMinioBucketName)
            .NotEmpty()
            .When(x => !x.InheritMinioBucketName)
            .WithMessage("禁用继承时必须提供应用级 MinIO Bucket。");
    }
}

public sealed class WorkspaceCreateRequestValidator : AbstractValidator<WorkspaceCreateRequest>
{
    public WorkspaceCreateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Description)
            .MaximumLength(1024)
            .When(x => x.Description is not null);

        RuleFor(x => x.Icon)
            .MaximumLength(256)
            .When(x => x.Icon is not null);
    }
}

public sealed class WorkspaceUpdateRequestValidator : AbstractValidator<WorkspaceUpdateRequest>
{
    public WorkspaceUpdateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Description)
            .MaximumLength(1024)
            .When(x => x.Description is not null);

        RuleFor(x => x.Icon)
            .MaximumLength(256)
            .When(x => x.Icon is not null);
    }
}

public sealed class WorkspaceAppInstanceCreateRequestValidator : AbstractValidator<WorkspaceAppInstanceCreateRequest>
{
    public WorkspaceAppInstanceCreateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Description)
            .MaximumLength(1024)
            .When(x => x.Description is not null);

        RuleFor(x => x.Icon)
            .MaximumLength(256)
            .When(x => x.Icon is not null);

        RuleFor(x => x.Category)
            .MaximumLength(64)
            .When(x => x.Category is not null);

        // 1→N 模型：AppKey 可为空，留空时由 Service 层按 workspace 自动生成。
        RuleFor(x => x.AppKey)
            .MaximumLength(64)
            .Matches(@"^[A-Za-z][A-Za-z0-9_-]*$")
            .When(x => !string.IsNullOrWhiteSpace(x.AppKey))
            .WithMessage("AppKey 必须以字母开头，仅包含字母、数字、下划线和短横线。");
    }
}

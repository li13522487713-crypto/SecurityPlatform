using Atlas.Application.LowCode.Models;
using Atlas.Application.Resources;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace Atlas.Application.LowCode.Validators;

public sealed class LowCodeAppCreateRequestValidator : AbstractValidator<LowCodeAppCreateRequest>
{
    public LowCodeAppCreateRequestValidator(IStringLocalizer<Messages> localizer)
    {
        RuleFor(x => x.AppKey)
            .NotEmpty().WithMessage(localizer["LowCodeAppKeyRequired"].Value)
            .MaximumLength(100).WithMessage(localizer["LowCodeAppKeyMaxLength"].Value)
            .Matches(@"^[a-zA-Z][a-zA-Z0-9_-]*$").WithMessage(localizer["LowCodeAppKeyFormatInvalid"].Value);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(localizer["LowCodeAppNameRequired"].Value)
            .MaximumLength(200).WithMessage(localizer["LowCodeAppNameMaxLength"].Value);

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage(localizer["LowCodeAppDescriptionMaxLength"].Value);

        RuleFor(x => x.Category)
            .MaximumLength(100).WithMessage(localizer["LowCodeAppCategoryMaxLength"].Value);

        RuleFor(x => x.DataSourceId)
            .GreaterThan(0).WithMessage("DataSourceId 必须为正整数")
            .When(x => x.DataSourceId.HasValue);
    }
}

public sealed class LowCodeAppUpdateRequestValidator : AbstractValidator<LowCodeAppUpdateRequest>
{
    public LowCodeAppUpdateRequestValidator(IStringLocalizer<Messages> localizer)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(localizer["LowCodeAppNameRequired"].Value)
            .MaximumLength(200).WithMessage(localizer["LowCodeAppNameMaxLength"].Value);

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage(localizer["LowCodeAppDescriptionMaxLength"].Value);

        RuleFor(x => x.Category)
            .MaximumLength(100).WithMessage(localizer["LowCodeAppCategoryMaxLength"].Value);
    }
}

public sealed class LowCodeAppImportRequestValidator : AbstractValidator<LowCodeAppImportRequest>
{
    private static readonly string[] ConflictStrategies = ["Rename", "Overwrite", "Skip"];

    public LowCodeAppImportRequestValidator()
    {
        RuleFor(x => x.Package)
            .NotNull().WithMessage("导入包不能为空");

        RuleFor(x => x.Package != null ? x.Package.AppKey : null)
            .NotEmpty().WithMessage("导入包应用标识不能为空")
            .MaximumLength(100).WithMessage("导入包应用标识不能超过100个字符")
            .Matches(@"^[a-zA-Z][a-zA-Z0-9_-]*$").WithMessage("导入包应用标识格式非法")
            .When(x => x.Package is not null);

        RuleFor(x => x.Package != null ? x.Package.Name : null)
            .NotEmpty().WithMessage("导入包应用名称不能为空")
            .MaximumLength(200).WithMessage("导入包应用名称不能超过200个字符")
            .When(x => x.Package is not null);

        RuleFor(x => x.ConflictStrategy)
            .NotEmpty().WithMessage("冲突策略不能为空")
            .Must(x => ConflictStrategies.Contains(x, StringComparer.OrdinalIgnoreCase))
            .WithMessage("冲突策略仅支持 Rename/Overwrite/Skip");

        RuleFor(x => x.KeySuffix)
            .MaximumLength(32).WithMessage("后缀长度不能超过32")
            .Matches(@"^[a-zA-Z0-9_-]+$").WithMessage("后缀仅支持字母数字下划线连字符")
            .When(x => !string.IsNullOrWhiteSpace(x.KeySuffix));
    }
}

public sealed class LowCodeAppSharingPolicyUpdateRequestValidator : AbstractValidator<LowCodeAppSharingPolicyUpdateRequest>
{
    public LowCodeAppSharingPolicyUpdateRequestValidator()
    {
        RuleFor(x => x).NotNull();
    }
}

public sealed class LowCodeAppEntityAliasesUpdateRequestValidator : AbstractValidator<LowCodeAppEntityAliasesUpdateRequest>
{
    public LowCodeAppEntityAliasesUpdateRequestValidator()
    {
        RuleFor(x => x.Items)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("实体别名集合不能为空")
            .Must(x => x.Count <= 20).WithMessage("实体别名数量不能超过20");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.EntityType)
                .NotEmpty().WithMessage("实体类型不能为空")
                .MaximumLength(64).WithMessage("实体类型长度不能超过64");

            item.RuleFor(i => i.SingularAlias)
                .NotEmpty().WithMessage("单数别名不能为空")
                .MaximumLength(64).WithMessage("单数别名长度不能超过64");

            item.RuleFor(i => i.PluralAlias)
                .NotEmpty().WithMessage("复数别名不能为空")
                .MaximumLength(64).WithMessage("复数别名长度不能超过64");
        });
    }
}

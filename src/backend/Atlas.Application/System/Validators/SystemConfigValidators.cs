using FluentValidation;
using Atlas.Application.System.Models;
using Atlas.Application.Resources;
using Microsoft.Extensions.Localization;
using global::System.Text.RegularExpressions;

namespace Atlas.Application.System.Validators;

public sealed class SystemConfigCreateRequestValidator : AbstractValidator<SystemConfigCreateRequest>
{
    internal static readonly Regex SafeKeyPattern =
        new(@"^[a-zA-Z][a-zA-Z0-9_.:\-]{0,127}$", RegexOptions.Compiled);

    public SystemConfigCreateRequestValidator(IStringLocalizer<Messages> localizer)
    {
        RuleFor(x => x.ConfigKey)
            .NotEmpty()
            .MaximumLength(128)
            .Must(k => SafeKeyPattern.IsMatch(k))
            .WithMessage(localizer["SystemConfigKeyFormat"].Value);
        RuleFor(x => x.ConfigValue).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.ConfigName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Remark).MaximumLength(500).When(x => x.Remark != null);
        RuleFor(x => x.GroupName).MaximumLength(100).When(x => x.GroupName != null);
        RuleFor(x => x.AppId).MaximumLength(64).When(x => x.AppId != null);
    }
}

public sealed class SystemConfigUpdateRequestValidator : AbstractValidator<SystemConfigUpdateRequest>
{
    public SystemConfigUpdateRequestValidator()
    {
        RuleFor(x => x.ConfigValue).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.ConfigName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Remark).MaximumLength(500).When(x => x.Remark != null);
        RuleFor(x => x.GroupName).MaximumLength(100).When(x => x.GroupName != null);
        RuleFor(x => x.Version)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Version.HasValue);
    }
}

public sealed class SystemConfigBatchUpsertRequestValidator : AbstractValidator<SystemConfigBatchUpsertRequest>
{
    public SystemConfigBatchUpsertRequestValidator(IStringLocalizer<Messages> localizer)
    {
        RuleFor(x => x.Items)
            .NotEmpty()
            .Must(items => items.Count <= 200)
            .WithMessage("批量更新一次最多支持 200 项。");
        RuleForEach(x => x.Items).SetValidator(new SystemConfigBatchUpsertItemValidator(localizer));
        RuleFor(x => x.AppId).MaximumLength(64).When(x => x.AppId != null);
        RuleFor(x => x.GroupName).MaximumLength(100).When(x => x.GroupName != null);
    }
}

public sealed class SystemConfigBatchUpsertItemValidator : AbstractValidator<SystemConfigBatchUpsertItem>
{
    public SystemConfigBatchUpsertItemValidator(IStringLocalizer<Messages> localizer)
    {
        RuleFor(x => x.ConfigKey)
            .NotEmpty()
            .MaximumLength(128)
            .Must(k => SystemConfigCreateRequestValidator.SafeKeyPattern.IsMatch(k))
            .WithMessage(localizer["SystemConfigKeyFormat"].Value);
        RuleFor(x => x.ConfigValue).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.ConfigName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Remark).MaximumLength(500).When(x => x.Remark != null);
        RuleFor(x => x.ConfigType).NotEmpty().MaximumLength(32);
        RuleFor(x => x.AppId).MaximumLength(64).When(x => x.AppId != null);
        RuleFor(x => x.GroupName).MaximumLength(100).When(x => x.GroupName != null);
        RuleFor(x => x.Version)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Version.HasValue);
    }
}

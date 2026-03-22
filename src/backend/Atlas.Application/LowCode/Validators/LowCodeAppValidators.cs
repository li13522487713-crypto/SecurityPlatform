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
            .GreaterThan(0).WithMessage(localizer["LowCodeDataSourceIdPositive"].Value)
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

        RuleFor(x => x.DataSourceId)
            .GreaterThan(0).WithMessage(localizer["LowCodeDataSourceIdPositive"].Value)
            .When(x => x.DataSourceId.HasValue);
    }
}

public sealed class LowCodeAppImportRequestValidator : AbstractValidator<LowCodeAppImportRequest>
{
    private static readonly string[] ConflictStrategies = ["Rename", "Overwrite", "Skip"];

    public LowCodeAppImportRequestValidator(IStringLocalizer<Messages> localizer)
    {
        RuleFor(x => x.Package)
            .NotNull().WithMessage(localizer["LowCodeImportPackageRequired"].Value);

        RuleFor(x => x.Package != null ? x.Package.AppKey : null)
            .NotEmpty().WithMessage(localizer["LowCodeImportAppKeyRequired"].Value)
            .MaximumLength(100).WithMessage(localizer["LowCodeImportAppKeyMaxLength", 100].Value)
            .Matches(@"^[a-zA-Z][a-zA-Z0-9_-]*$").WithMessage(localizer["LowCodeImportAppKeyInvalid"].Value)
            .When(x => x.Package is not null);

        RuleFor(x => x.Package != null ? x.Package.Name : null)
            .NotEmpty().WithMessage(localizer["LowCodeImportNameRequired"].Value)
            .MaximumLength(200).WithMessage(localizer["LowCodeImportNameMaxLength", 200].Value)
            .When(x => x.Package is not null);

        RuleFor(x => x.ConflictStrategy)
            .NotEmpty().WithMessage(localizer["LowCodeImportConflictStrategyRequired"].Value)
            .Must(x => ConflictStrategies.Contains(x, StringComparer.OrdinalIgnoreCase))
            .WithMessage(localizer["LowCodeImportConflictStrategyInvalid"].Value);

        RuleFor(x => x.KeySuffix)
            .MaximumLength(32).WithMessage(localizer["LowCodeImportKeySuffixMaxLength", 32].Value)
            .Matches(@"^[a-zA-Z0-9_-]+$").WithMessage(localizer["LowCodeImportKeySuffixInvalid"].Value)
            .When(x => !string.IsNullOrWhiteSpace(x.KeySuffix));
    }
}

public sealed class LowCodeAppEntityAliasesUpdateRequestValidator : AbstractValidator<LowCodeAppEntityAliasesUpdateRequest>
{
    public LowCodeAppEntityAliasesUpdateRequestValidator(IStringLocalizer<Messages> localizer)
    {
        RuleFor(x => x.Items)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(localizer["LowCodeEntityAliasesRequired"].Value)
            .Must(x => x.Count <= 20).WithMessage(localizer["LowCodeEntityAliasesCountMax", 20].Value);

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.EntityType)
                .NotEmpty().WithMessage(localizer["LowCodeEntityTypeRequired"].Value)
                .MaximumLength(64).WithMessage(localizer["LowCodeEntityTypeMaxLength", 64].Value);

            item.RuleFor(i => i.SingularAlias)
                .NotEmpty().WithMessage(localizer["LowCodeSingularAliasRequired"].Value)
                .MaximumLength(64).WithMessage(localizer["LowCodeSingularAliasMaxLength", 64].Value);

            item.RuleFor(i => i.PluralAlias)
                .NotEmpty().WithMessage(localizer["LowCodePluralAliasRequired"].Value)
                .MaximumLength(64).WithMessage(localizer["LowCodePluralAliasMaxLength", 64].Value);
        });
    }
}

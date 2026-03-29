using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Resources;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace Atlas.Application.AiPlatform.Validators;

public sealed class ModelConfigCreateRequestValidator : AbstractValidator<ModelConfigCreateRequest>
{
    private static readonly string[] AllowedProviderTypes = ["openai", "deepseek", "ollama", "custom"];

    public ModelConfigCreateRequestValidator(IStringLocalizer<Messages> localizer)
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.ProviderType)
            .NotEmpty()
            .Must(x => AllowedProviderTypes.Contains(x, StringComparer.OrdinalIgnoreCase))
            .WithMessage(localizer["ModelConfigProviderTypeInvalid"].Value);
        RuleFor(x => x.ApiKey).NotEmpty().MaximumLength(4096);
        RuleFor(x => x.BaseUrl).NotEmpty().MaximumLength(512).Must(IsValidAbsoluteUrl).WithMessage(localizer["ModelConfigBaseUrlInvalid"].Value);
        RuleFor(x => x.DefaultModel).NotEmpty().MaximumLength(256);
    }

    private static bool IsValidAbsoluteUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out _);
}

public sealed class ModelConfigUpdateRequestValidator : AbstractValidator<ModelConfigUpdateRequest>
{
    public ModelConfigUpdateRequestValidator(IStringLocalizer<Messages> localizer)
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.ApiKey).NotEmpty().MaximumLength(4096);
        RuleFor(x => x.BaseUrl).NotEmpty().MaximumLength(512).Must(IsValidAbsoluteUrl).WithMessage(localizer["ModelConfigBaseUrlInvalid"].Value);
        RuleFor(x => x.DefaultModel).NotEmpty().MaximumLength(256);
    }

    private static bool IsValidAbsoluteUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out _);
}

public sealed class ModelConfigTestRequestValidator : AbstractValidator<ModelConfigTestRequest>
{
    public ModelConfigTestRequestValidator(IStringLocalizer<Messages> localizer)
    {
        RuleFor(x => x.ProviderType).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ApiKey).NotEmpty().MaximumLength(4096);
        RuleFor(x => x.BaseUrl).NotEmpty().MaximumLength(512).Must(IsValidAbsoluteUrl).WithMessage(localizer["ModelConfigBaseUrlInvalid"].Value);
        RuleFor(x => x.Model).NotEmpty().MaximumLength(256);
    }

    private static bool IsValidAbsoluteUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out _);
}

public sealed class ModelConfigPromptTestRequestValidator : AbstractValidator<ModelConfigPromptTestRequest>
{
    public ModelConfigPromptTestRequestValidator(IStringLocalizer<Messages> localizer)
    {
        RuleFor(x => x.ProviderType).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ApiKey).NotEmpty().MaximumLength(4096);
        RuleFor(x => x.BaseUrl).NotEmpty().MaximumLength(512).Must(IsValidAbsoluteUrl).WithMessage(localizer["ModelConfigBaseUrlInvalid"].Value);
        RuleFor(x => x.Model).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Prompt).NotEmpty().MaximumLength(4000);
    }

    private static bool IsValidAbsoluteUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out _);
}

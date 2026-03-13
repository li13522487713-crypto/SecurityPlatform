using Atlas.Application.AiPlatform.Models;
using FluentValidation;

namespace Atlas.Application.AiPlatform.Validators;

public sealed class ModelConfigCreateRequestValidator : AbstractValidator<ModelConfigCreateRequest>
{
    private static readonly string[] AllowedProviderTypes = ["openai", "deepseek", "ollama", "custom"];

    public ModelConfigCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.ProviderType)
            .NotEmpty()
            .Must(x => AllowedProviderTypes.Contains(x, StringComparer.OrdinalIgnoreCase))
            .WithMessage("ProviderType 仅支持 openai/deepseek/ollama/custom。");
        RuleFor(x => x.ApiKey).NotEmpty().MaximumLength(4096);
        RuleFor(x => x.BaseUrl).NotEmpty().MaximumLength(512).Must(IsValidAbsoluteUrl).WithMessage("BaseUrl 必须是合法绝对地址。");
        RuleFor(x => x.DefaultModel).NotEmpty().MaximumLength(256);
    }

    private static bool IsValidAbsoluteUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out _);
}

public sealed class ModelConfigUpdateRequestValidator : AbstractValidator<ModelConfigUpdateRequest>
{
    public ModelConfigUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.ApiKey).NotEmpty().MaximumLength(4096);
        RuleFor(x => x.BaseUrl).NotEmpty().MaximumLength(512).Must(IsValidAbsoluteUrl).WithMessage("BaseUrl 必须是合法绝对地址。");
        RuleFor(x => x.DefaultModel).NotEmpty().MaximumLength(256);
    }

    private static bool IsValidAbsoluteUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out _);
}

public sealed class ModelConfigTestRequestValidator : AbstractValidator<ModelConfigTestRequest>
{
    public ModelConfigTestRequestValidator()
    {
        RuleFor(x => x.ProviderType).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ApiKey).NotEmpty().MaximumLength(4096);
        RuleFor(x => x.BaseUrl).NotEmpty().MaximumLength(512).Must(IsValidAbsoluteUrl).WithMessage("BaseUrl 必须是合法绝对地址。");
        RuleFor(x => x.Model).NotEmpty().MaximumLength(256);
    }

    private static bool IsValidAbsoluteUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out _);
}

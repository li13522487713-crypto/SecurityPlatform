using FluentValidation;
using Atlas.Application.LowCode.Models;

namespace Atlas.Application.LowCode.Validators;

public sealed class AppContentParamCreateRequestValidator : AbstractValidator<AppContentParamCreateRequest>
{
    private static readonly HashSet<string> AllowedKinds = new(StringComparer.Ordinal)
    {
        "text", "image", "data", "link", "media", "ai"
    };

    public AppContentParamCreateRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Matches(LowCodeCodeRule.Pattern);
        RuleFor(x => x.Kind).NotEmpty()
            .Must(k => AllowedKinds.Contains(k))
            .WithMessage("内容参数 kind 必须为 6 类之一：text/image/data/link/media/ai（docx §U11）");
        RuleFor(x => x.ConfigJson).NotEmpty().MaximumLength(200_000);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class AppContentParamUpdateRequestValidator : AbstractValidator<AppContentParamUpdateRequest>
{
    private static readonly HashSet<string> AllowedKinds = new(StringComparer.Ordinal)
    {
        "text", "image", "data", "link", "media", "ai"
    };

    public AppContentParamUpdateRequestValidator()
    {
        RuleFor(x => x.Kind).NotEmpty()
            .Must(k => AllowedKinds.Contains(k));
        RuleFor(x => x.ConfigJson).NotEmpty().MaximumLength(200_000);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

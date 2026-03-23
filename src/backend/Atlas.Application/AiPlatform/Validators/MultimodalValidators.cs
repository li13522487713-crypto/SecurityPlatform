using Atlas.Application.AiPlatform.Models;
using FluentValidation;

namespace Atlas.Application.AiPlatform.Validators;

public sealed class MultimodalAssetCreateRequestValidator : AbstractValidator<MultimodalAssetCreateRequest>
{
    public MultimodalAssetCreateRequestValidator()
    {
        RuleFor(x => x.AssetType).IsInEnum();
        RuleFor(x => x.SourceType).IsInEnum();
        RuleFor(x => x.Name).MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.Name));
        RuleFor(x => x.MimeType).MaximumLength(128).When(x => !string.IsNullOrWhiteSpace(x.MimeType));
        RuleFor(x => x.FileId).MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.FileId));
        RuleFor(x => x.SourceUrl).MaximumLength(1024).When(x => !string.IsNullOrWhiteSpace(x.SourceUrl));
        RuleFor(x => x.ContentText).MaximumLength(32000).When(x => !string.IsNullOrWhiteSpace(x.ContentText));
        RuleFor(x => x).Must(x =>
                !string.IsNullOrWhiteSpace(x.FileId)
                || !string.IsNullOrWhiteSpace(x.SourceUrl)
                || !string.IsNullOrWhiteSpace(x.ContentText))
            .WithMessage("fileId/sourceUrl/contentText 至少需要一个。");
    }
}

public sealed class VisionAnalyzeRequestValidator : AbstractValidator<VisionAnalyzeRequest>
{
    public VisionAnalyzeRequestValidator()
    {
        RuleFor(x => x.AssetId).GreaterThan(0).When(x => x.AssetId.HasValue);
        RuleFor(x => x.ImageUrl).MaximumLength(1024).When(x => !string.IsNullOrWhiteSpace(x.ImageUrl));
        RuleFor(x => x.Prompt).MaximumLength(2000).When(x => !string.IsNullOrWhiteSpace(x.Prompt));
        RuleFor(x => x).Must(x => x.AssetId.HasValue || !string.IsNullOrWhiteSpace(x.ImageUrl))
            .WithMessage("assetId 或 imageUrl 至少需要一个。");
    }
}

public sealed class AsrTranscribeRequestValidator : AbstractValidator<AsrTranscribeRequest>
{
    public AsrTranscribeRequestValidator()
    {
        RuleFor(x => x.AssetId).GreaterThan(0).When(x => x.AssetId.HasValue);
        RuleFor(x => x.AudioUrl).MaximumLength(1024).When(x => !string.IsNullOrWhiteSpace(x.AudioUrl));
        RuleFor(x => x.LanguageHint).MaximumLength(32).When(x => !string.IsNullOrWhiteSpace(x.LanguageHint));
        RuleFor(x => x.Prompt).MaximumLength(2000).When(x => !string.IsNullOrWhiteSpace(x.Prompt));
        RuleFor(x => x).Must(x => x.AssetId.HasValue || !string.IsNullOrWhiteSpace(x.AudioUrl))
            .WithMessage("assetId 或 audioUrl 至少需要一个。");
    }
}

public sealed class TtsSynthesizeRequestValidator : AbstractValidator<TtsSynthesizeRequest>
{
    public TtsSynthesizeRequestValidator()
    {
        RuleFor(x => x.Text).NotEmpty().MaximumLength(8000);
        RuleFor(x => x.Voice).MaximumLength(64).When(x => !string.IsNullOrWhiteSpace(x.Voice));
        RuleFor(x => x.Format).MaximumLength(16).When(x => !string.IsNullOrWhiteSpace(x.Format));
        RuleFor(x => x.Language).MaximumLength(16).When(x => !string.IsNullOrWhiteSpace(x.Language));
    }
}

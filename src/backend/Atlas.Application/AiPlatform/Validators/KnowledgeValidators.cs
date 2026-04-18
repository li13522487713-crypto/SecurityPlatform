using Atlas.Application.AiPlatform.Models;
using FluentValidation;

namespace Atlas.Application.AiPlatform.Validators;

public sealed class KnowledgeBaseCreateRequestValidator : AbstractValidator<KnowledgeBaseCreateRequest>
{
    public KnowledgeBaseCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.WorkspaceId).GreaterThan(0).When(x => x.WorkspaceId.HasValue);
    }
}

public sealed class KnowledgeBaseUpdateRequestValidator : AbstractValidator<KnowledgeBaseUpdateRequest>
{
    public KnowledgeBaseUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.WorkspaceId).GreaterThan(0).When(x => x.WorkspaceId.HasValue);
    }
}

public sealed class DocumentCreateRequestValidator : AbstractValidator<DocumentCreateRequest>
{
    public DocumentCreateRequestValidator()
    {
        RuleFor(x => x.FileId).GreaterThan(0);
        RuleFor(x => x.TagsJson).MaximumLength(16_000).When(x => !string.IsNullOrWhiteSpace(x.TagsJson));
        RuleFor(x => x.ImageMetadataJson).MaximumLength(32_000).When(x => !string.IsNullOrWhiteSpace(x.ImageMetadataJson));
    }
}

public sealed class DocumentResegmentRequestValidator : AbstractValidator<DocumentResegmentRequest>
{
    public DocumentResegmentRequestValidator()
    {
        RuleFor(x => x.ChunkSize).GreaterThanOrEqualTo(50).LessThanOrEqualTo(4000);
        RuleFor(x => x.Overlap).GreaterThanOrEqualTo(0).LessThan(1000);
        RuleFor(x => x.Overlap).LessThan(x => x.ChunkSize);
        RuleFor(x => x.Strategy).IsInEnum();
        RuleFor(x => x.ParseStrategy).IsInEnum();
    }
}

public sealed class ChunkCreateRequestValidator : AbstractValidator<ChunkCreateRequest>
{
    public ChunkCreateRequestValidator()
    {
        RuleFor(x => x.DocumentId).GreaterThan(0);
        RuleFor(x => x.ChunkIndex).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Content).NotEmpty().MaximumLength(32000);
        RuleFor(x => x.StartOffset).GreaterThanOrEqualTo(0);
        RuleFor(x => x.EndOffset).GreaterThanOrEqualTo(x => x.StartOffset);
    }
}

public sealed class ChunkUpdateRequestValidator : AbstractValidator<ChunkUpdateRequest>
{
    public ChunkUpdateRequestValidator()
    {
        RuleFor(x => x.Content).NotEmpty().MaximumLength(32000);
        RuleFor(x => x.StartOffset).GreaterThanOrEqualTo(0);
        RuleFor(x => x.EndOffset).GreaterThanOrEqualTo(x => x.StartOffset);
    }
}

public sealed class KnowledgeRetrievalTestRequestValidator : AbstractValidator<KnowledgeRetrievalTestRequest>
{
    public KnowledgeRetrievalTestRequestValidator()
    {
        RuleFor(x => x.Query).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.TopK).GreaterThanOrEqualTo(1).LessThanOrEqualTo(20);
        RuleFor(x => x.Offset).GreaterThanOrEqualTo(0).LessThanOrEqualTo(500);
        RuleFor(x => x.MinScore).InclusiveBetween(0f, 1f).When(x => x.MinScore.HasValue);
        RuleForEach(x => x.Tags!).MaximumLength(128).When(x => x.Tags is not null);
        RuleFor(x => x.Tags!.Count).LessThanOrEqualTo(32).When(x => x.Tags is not null);
        RuleFor(x => x.KnowledgeBaseIds!.Count).LessThanOrEqualTo(32).When(x => x.KnowledgeBaseIds is not null);
        RuleFor(x => x.OwnerFilter).MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.OwnerFilter));
    }
}

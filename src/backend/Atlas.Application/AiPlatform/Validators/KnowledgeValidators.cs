using Atlas.Application.AiPlatform.Models;
using FluentValidation;

namespace Atlas.Application.AiPlatform.Validators;

public sealed class KnowledgeBaseCreateRequestValidator : AbstractValidator<KnowledgeBaseCreateRequest>
{
    public KnowledgeBaseCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}

public sealed class KnowledgeBaseUpdateRequestValidator : AbstractValidator<KnowledgeBaseUpdateRequest>
{
    public KnowledgeBaseUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}

public sealed class DocumentCreateRequestValidator : AbstractValidator<DocumentCreateRequest>
{
    public DocumentCreateRequestValidator()
    {
        RuleFor(x => x.FileId).GreaterThan(0);
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

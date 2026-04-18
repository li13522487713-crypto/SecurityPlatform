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

/* -------------------------------------------------------------------------- */
/*                v5 §32-44 新增请求类型 FluentValidation                       */
/* -------------------------------------------------------------------------- */

public sealed class RetrievalRequestValidator : AbstractValidator<RetrievalRequest>
{
    public RetrievalRequestValidator()
    {
        RuleFor(x => x.Query).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.TopK).GreaterThanOrEqualTo(1).LessThanOrEqualTo(50);
        RuleFor(x => x.MinScore).InclusiveBetween(0f, 1f).When(x => x.MinScore.HasValue);
        RuleFor(x => x.KnowledgeBaseIds).NotNull();
        RuleFor(x => x.KnowledgeBaseIds.Count).LessThanOrEqualTo(32).When(x => x.KnowledgeBaseIds is not null);
        RuleFor(x => x.CallerContext).NotNull();
        When(x => x.RetrievalProfile is not null, () =>
        {
            RuleFor(x => x.RetrievalProfile!.TopK).GreaterThanOrEqualTo(1).LessThanOrEqualTo(50);
            RuleFor(x => x.RetrievalProfile!.MinScore).InclusiveBetween(0f, 1f);
        });
    }
}

public sealed class KnowledgeJobsListRequestValidator : AbstractValidator<KnowledgeJobsListRequest>
{
    public KnowledgeJobsListRequestValidator()
    {
        RuleFor(x => x.PageIndex).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}

public sealed class KnowledgeBindingCreateRequestValidator : AbstractValidator<KnowledgeBindingCreateRequest>
{
    public KnowledgeBindingCreateRequestValidator()
    {
        RuleFor(x => x.CallerType).IsInEnum();
        RuleFor(x => x.CallerId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.CallerName).NotEmpty().MaximumLength(256);
    }
}

public sealed class KnowledgePermissionGrantRequestValidator : AbstractValidator<KnowledgePermissionGrantRequest>
{
    public KnowledgePermissionGrantRequestValidator()
    {
        RuleFor(x => x.Scope).IsInEnum();
        RuleFor(x => x.SubjectType).IsInEnum();
        RuleFor(x => x.SubjectId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.SubjectName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.ScopeId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Actions).NotEmpty();
        RuleForEach(x => x.Actions).IsInEnum();
    }
}

public sealed class KnowledgeVersionCreateRequestValidator : AbstractValidator<KnowledgeVersionCreateRequest>
{
    public KnowledgeVersionCreateRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Note).MaximumLength(2000).When(x => !string.IsNullOrWhiteSpace(x.Note));
    }
}

public sealed class RerunParseRequestValidator : AbstractValidator<RerunParseRequest>
{
    public RerunParseRequestValidator()
    {
        RuleFor(x => x.DocumentId).GreaterThan(0);
    }
}

public sealed class RebuildIndexRequestValidator : AbstractValidator<RebuildIndexRequest>
{
    public RebuildIndexRequestValidator()
    {
        RuleFor(x => x.DocumentId).GreaterThan(0).When(x => x.DocumentId.HasValue);
    }
}

using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class KnowledgeReview : TenantEntity
{
    public KnowledgeReview() : base(TenantId.Empty)
    {
        ReviewJson = "{}";
        Status = string.Empty;
    }

    public long KnowledgeBaseId { get; private set; }
    public long DocumentId { get; private set; }
    public string ReviewJson { get; private set; }
    public string Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
}

public sealed class KnowledgeSlice : TenantEntity
{
    public KnowledgeSlice() : base(TenantId.Empty)
    {
        Content = string.Empty;
        EmbeddingStatus = string.Empty;
    }

    public long KnowledgeBaseId { get; private set; }
    public long DocumentId { get; private set; }
    public int SliceIndex { get; private set; }
    public string Content { get; private set; }
    public int StartOffset { get; private set; }
    public int EndOffset { get; private set; }
    public string EmbeddingStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
}

public sealed class KnowledgeImportTask : TenantEntity
{
    public KnowledgeImportTask() : base(TenantId.Empty)
    {
        Status = string.Empty;
        RequestJson = "{}";
        ResultJson = "{}";
    }

    public long KnowledgeBaseId { get; private set; }
    public string Status { get; private set; }
    public string RequestJson { get; private set; }
    public string ResultJson { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
}

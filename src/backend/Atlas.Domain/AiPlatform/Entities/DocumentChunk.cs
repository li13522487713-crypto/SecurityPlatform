using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class DocumentChunk : TenantEntity
{
    public DocumentChunk()
        : base(TenantId.Empty)
    {
        Content = string.Empty;
        CreatedAt = DateTime.UtcNow;
    }

    public DocumentChunk(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        int chunkIndex,
        string content,
        int startOffset,
        int endOffset,
        bool hasEmbedding,
        long id)
        : base(tenantId)
    {
        Id = id;
        KnowledgeBaseId = knowledgeBaseId;
        DocumentId = documentId;
        ChunkIndex = chunkIndex;
        Content = content;
        StartOffset = startOffset;
        EndOffset = endOffset;
        HasEmbedding = hasEmbedding;
        CreatedAt = DateTime.UtcNow;
    }

    public long KnowledgeBaseId { get; private set; }
    public long DocumentId { get; private set; }
    public int ChunkIndex { get; private set; }
    public string Content { get; private set; }
    public int StartOffset { get; private set; }
    public int EndOffset { get; private set; }
    public bool HasEmbedding { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public void UpdateContent(string content, int startOffset, int endOffset)
    {
        Content = content;
        StartOffset = startOffset;
        EndOffset = endOffset;
    }

    public void MarkEmbedding(bool hasEmbedding)
    {
        HasEmbedding = hasEmbedding;
    }
}

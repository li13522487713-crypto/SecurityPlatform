using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class DocumentChunk : TenantEntity
{
    public DocumentChunk()
        : base(TenantId.Empty)
    {
        Content = string.Empty;
        ColumnHeadersJson = "[]";
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
        long id,
        int? rowIndex = null,
        string? columnHeadersJson = null)
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
        RowIndex = rowIndex ?? 0;
        ColumnHeadersJson = string.IsNullOrWhiteSpace(columnHeadersJson) ? "[]" : columnHeadersJson;
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

    /// <summary>1-based table row index when knowledge base type is table; null for text/image chunks.</summary>
    public int RowIndex { get; private set; }

    /// <summary>JSON array of column header strings shared by table row chunks.</summary>
    public string ColumnHeadersJson { get; private set; }

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

using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class KnowledgeDocument : TenantEntity
{
    public KnowledgeDocument()
        : base(TenantId.Empty)
    {
        FileName = string.Empty;
        CreatedAt = DateTime.UtcNow;
    }

    public KnowledgeDocument(
        TenantId tenantId,
        long knowledgeBaseId,
        long? fileId,
        string fileName,
        string? contentType,
        long fileSizeBytes,
        long id)
        : base(tenantId)
    {
        Id = id;
        KnowledgeBaseId = knowledgeBaseId;
        FileId = fileId;
        FileName = fileName;
        ContentType = contentType ?? string.Empty;
        FileSizeBytes = fileSizeBytes;
        Status = DocumentProcessingStatus.Pending;
        ChunkCount = 0;
        CreatedAt = DateTime.UtcNow;
    }

    public long KnowledgeBaseId { get; private set; }
    public long? FileId { get; private set; }
    public string FileName { get; private set; }
    public string? ContentType { get; private set; }
    public long FileSizeBytes { get; private set; }
    public DocumentProcessingStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int ChunkCount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }

    public void MarkProcessing()
    {
        Status = DocumentProcessingStatus.Processing;
        ErrorMessage = null;
        ProcessedAt = null;
    }

    public void MarkCompleted(int chunkCount)
    {
        Status = DocumentProcessingStatus.Completed;
        ChunkCount = Math.Max(0, chunkCount);
        ErrorMessage = null;
        ProcessedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string error)
    {
        Status = DocumentProcessingStatus.Failed;
        ErrorMessage = error;
        ProcessedAt = DateTime.UtcNow;
    }
}

public enum DocumentProcessingStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}

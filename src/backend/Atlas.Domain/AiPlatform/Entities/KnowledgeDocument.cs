using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class KnowledgeDocument : TenantEntity
{
    public KnowledgeDocument()
        : base(TenantId.Empty)
    {
        FileName = string.Empty;
        TagsJson = "[]";
        ImageMetadataJson = "{}";
        CreatedAt = DateTime.UtcNow;
    }

    public KnowledgeDocument(
        TenantId tenantId,
        long knowledgeBaseId,
        long? fileId,
        string fileName,
        string? contentType,
        long fileSizeBytes,
        long id,
        string? tagsJson = null,
        string? imageMetadataJson = null)
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
        TagsJson = NormalizeTagsJson(tagsJson);
        ImageMetadataJson = NormalizeImageMetadataJson(imageMetadataJson);
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

    /// <summary>JSON array of tag strings, e.g. <c>["a","b"]</c>.</summary>
    public string TagsJson { get; private set; } = "[]";

    /// <summary>JSON object for image annotations / OCR metadata (image knowledge bases).</summary>
    public string ImageMetadataJson { get; private set; } = "{}";

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

    public void SetTagsAndImageMetadata(string? tagsJson, string? imageMetadataJson)
    {
        TagsJson = NormalizeTagsJson(tagsJson);
        ImageMetadataJson = NormalizeImageMetadataJson(imageMetadataJson);
    }

    private static string NormalizeTagsJson(string? tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson))
        {
            return "[]";
        }

        var t = tagsJson.Trim();
        return t.Length == 0 ? "[]" : t;
    }

    private static string NormalizeImageMetadataJson(string? imageMetadataJson)
    {
        if (string.IsNullOrWhiteSpace(imageMetadataJson))
        {
            return "{}";
        }

        var t = imageMetadataJson.Trim();
        return t.Length == 0 ? "{}" : t;
    }
}

public enum DocumentProcessingStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}

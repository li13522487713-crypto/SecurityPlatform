using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class MultimodalAsset : TenantEntity
{
    public MultimodalAsset()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        MimeType = string.Empty;
        FileId = string.Empty;
        SourceUrl = string.Empty;
        ContentText = string.Empty;
        MetadataJson = "{}";
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        Status = MultimodalAssetStatus.Pending;
        SourceType = MultimodalSourceType.Upload;
    }

    public MultimodalAsset(
        TenantId tenantId,
        long createdByUserId,
        MultimodalAssetType assetType,
        MultimodalSourceType sourceType,
        string? name,
        string? mimeType,
        string? fileId,
        string? sourceUrl,
        string? contentText,
        string? metadataJson,
        long id)
        : base(tenantId)
    {
        Id = id;
        CreatedByUserId = createdByUserId;
        AssetType = assetType;
        SourceType = sourceType;
        Name = name ?? string.Empty;
        MimeType = mimeType ?? string.Empty;
        FileId = fileId ?? string.Empty;
        SourceUrl = sourceUrl ?? string.Empty;
        ContentText = contentText ?? string.Empty;
        MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson;
        Status = MultimodalAssetStatus.Pending;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public MultimodalAssetType AssetType { get; private set; }
    public MultimodalSourceType SourceType { get; private set; }
    public MultimodalAssetStatus Status { get; private set; }
    public string Name { get; private set; }
    public string MimeType { get; private set; }
    public string FileId { get; private set; }
    public string SourceUrl { get; private set; }
    public string ContentText { get; private set; }
    public string MetadataJson { get; private set; }
    public long CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void MarkProcessed(string? contentText, string? metadataJson)
    {
        if (!string.IsNullOrWhiteSpace(contentText))
        {
            ContentText = contentText.Trim();
        }

        if (!string.IsNullOrWhiteSpace(metadataJson))
        {
            MetadataJson = metadataJson;
        }

        Status = MultimodalAssetStatus.Processed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string? metadataJson)
    {
        if (!string.IsNullOrWhiteSpace(metadataJson))
        {
            MetadataJson = metadataJson;
        }

        Status = MultimodalAssetStatus.Failed;
        UpdatedAt = DateTime.UtcNow;
    }
}

public enum MultimodalAssetType
{
    Image = 0,
    Audio = 1,
    Video = 2,
    Text = 3
}

public enum MultimodalSourceType
{
    Upload = 0,
    Url = 1,
    Generated = 2
}

public enum MultimodalAssetStatus
{
    Pending = 0,
    Processed = 1,
    Failed = 2
}

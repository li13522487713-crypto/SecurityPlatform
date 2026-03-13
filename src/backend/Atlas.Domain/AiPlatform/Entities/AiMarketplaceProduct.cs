using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AiMarketplaceProduct : TenantEntity
{
    public AiMarketplaceProduct()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        Summary = string.Empty;
        Description = string.Empty;
        Icon = string.Empty;
        TagsJson = "[]";
        Version = "0.1.0";
        CreatedAt = DateTime.UtcNow;
    }

    public AiMarketplaceProduct(
        TenantId tenantId,
        long categoryId,
        string name,
        string? summary,
        string? description,
        string? icon,
        string? tagsJson,
        AiMarketplaceProductType productType,
        long? sourceResourceId,
        long publisherUserId,
        long id)
        : base(tenantId)
    {
        Id = id;
        CategoryId = categoryId;
        Name = name;
        Summary = summary ?? string.Empty;
        Description = description ?? string.Empty;
        Icon = icon ?? string.Empty;
        TagsJson = string.IsNullOrWhiteSpace(tagsJson) ? "[]" : tagsJson;
        ProductType = productType;
        SourceResourceId = sourceResourceId;
        PublisherUserId = publisherUserId;
        Status = AiMarketplaceProductStatus.Draft;
        Version = "0.1.0";
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public long CategoryId { get; private set; }
    public string Name { get; private set; }
    public string? Summary { get; private set; }
    public string? Description { get; private set; }
    public string? Icon { get; private set; }
    public string TagsJson { get; private set; }
    public AiMarketplaceProductType ProductType { get; private set; }
    public long? SourceResourceId { get; private set; }
    public AiMarketplaceProductStatus Status { get; private set; }
    public string Version { get; private set; }
    public int DownloadCount { get; private set; }
    public int FavoriteCount { get; private set; }
    public long PublisherUserId { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public void Update(
        long categoryId,
        string name,
        string? summary,
        string? description,
        string? icon,
        string? tagsJson,
        AiMarketplaceProductType productType,
        long? sourceResourceId)
    {
        CategoryId = categoryId;
        Name = name;
        Summary = summary ?? string.Empty;
        Description = description ?? string.Empty;
        Icon = icon ?? string.Empty;
        TagsJson = string.IsNullOrWhiteSpace(tagsJson) ? "[]" : tagsJson;
        ProductType = productType;
        SourceResourceId = sourceResourceId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Publish(string version)
    {
        Status = AiMarketplaceProductStatus.Published;
        Version = version;
        PublishedAt = DateTime.UtcNow;
        UpdatedAt = PublishedAt;
    }

    public void Archive()
    {
        Status = AiMarketplaceProductStatus.Archived;
        UpdatedAt = DateTime.UtcNow;
    }

    public void IncreaseDownload()
    {
        DownloadCount += 1;
        UpdatedAt = DateTime.UtcNow;
    }

    public void IncreaseFavorite()
    {
        FavoriteCount += 1;
        UpdatedAt = DateTime.UtcNow;
    }

    public void DecreaseFavorite()
    {
        FavoriteCount = Math.Max(0, FavoriteCount - 1);
        UpdatedAt = DateTime.UtcNow;
    }
}

public enum AiMarketplaceProductType
{
    Agent = 1,
    Workflow = 2,
    Prompt = 3,
    Plugin = 4,
    App = 5
}

public enum AiMarketplaceProductStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2
}

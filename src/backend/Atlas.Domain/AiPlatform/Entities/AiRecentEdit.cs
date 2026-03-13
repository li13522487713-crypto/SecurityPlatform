using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AiRecentEdit : TenantEntity
{
    public AiRecentEdit()
        : base(TenantId.Empty)
    {
        ResourceType = string.Empty;
        ResourceTitle = string.Empty;
        ResourcePath = string.Empty;
        CreatedAt = DateTime.UtcNow;
    }

    public AiRecentEdit(
        TenantId tenantId,
        long userId,
        string resourceType,
        long resourceId,
        string resourceTitle,
        string resourcePath,
        long id)
        : base(tenantId)
    {
        Id = id;
        UserId = userId;
        ResourceType = resourceType;
        ResourceId = resourceId;
        ResourceTitle = resourceTitle;
        ResourcePath = resourcePath;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public long UserId { get; private set; }
    public string ResourceType { get; private set; }
    public long ResourceId { get; private set; }
    public string ResourceTitle { get; private set; }
    public string ResourcePath { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public void Refresh(string resourceTitle, string resourcePath)
    {
        ResourceTitle = resourceTitle;
        ResourcePath = resourcePath;
        UpdatedAt = DateTime.UtcNow;
    }
}

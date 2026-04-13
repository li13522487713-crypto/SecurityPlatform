using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class WorkspaceIdeFavorite : TenantEntity
{
    public WorkspaceIdeFavorite()
        : base(TenantId.Empty)
    {
        ResourceType = string.Empty;
        CreatedAt = DateTime.UtcNow;
    }

    public WorkspaceIdeFavorite(
        TenantId tenantId,
        long userId,
        string resourceType,
        long resourceId,
        long id)
        : base(tenantId)
    {
        Id = id;
        UserId = userId;
        ResourceType = resourceType;
        ResourceId = resourceId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public long UserId { get; private set; }
    public string ResourceType { get; private set; }
    public long ResourceId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public void Touch()
    {
        UpdatedAt = DateTime.UtcNow;
    }
}

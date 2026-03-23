using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class MultiAgentOrchestration : TenantEntity
{
    public MultiAgentOrchestration()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        Description = string.Empty;
        MembersJson = "[]";
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        Status = MultiAgentOrchestrationStatus.Draft;
    }

    public MultiAgentOrchestration(
        TenantId tenantId,
        string name,
        string? description,
        MultiAgentOrchestrationMode mode,
        string membersJson,
        long creatorUserId,
        long id)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        Description = description ?? string.Empty;
        Mode = mode;
        MembersJson = string.IsNullOrWhiteSpace(membersJson) ? "[]" : membersJson;
        CreatorUserId = creatorUserId;
        Status = MultiAgentOrchestrationStatus.Draft;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string Name { get; private set; }
    public string? Description { get; private set; }
    public MultiAgentOrchestrationMode Mode { get; private set; }
    public string MembersJson { get; private set; }
    public MultiAgentOrchestrationStatus Status { get; private set; }
    public long CreatorUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Update(
        string name,
        string? description,
        MultiAgentOrchestrationMode mode,
        string membersJson)
    {
        Name = name;
        Description = description ?? string.Empty;
        Mode = mode;
        MembersJson = string.IsNullOrWhiteSpace(membersJson) ? "[]" : membersJson;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        Status = MultiAgentOrchestrationStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Disable()
    {
        Status = MultiAgentOrchestrationStatus.Disabled;
        UpdatedAt = DateTime.UtcNow;
    }
}

public enum MultiAgentOrchestrationMode
{
    Sequential = 0,
    Parallel = 1
}

public enum MultiAgentOrchestrationStatus
{
    Draft = 0,
    Active = 1,
    Disabled = 2
}

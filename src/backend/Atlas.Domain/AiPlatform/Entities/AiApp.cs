using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AiApp : TenantEntity
{
    public AiApp()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        Description = string.Empty;
        Icon = string.Empty;
        CreatedAt = DateTime.UtcNow;
    }

    public AiApp(
        TenantId tenantId,
        string name,
        string? description,
        string? icon,
        long? agentId,
        long? workflowId,
        long? promptTemplateId,
        long id)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        Description = description ?? string.Empty;
        Icon = icon ?? string.Empty;
        AgentId = agentId;
        WorkflowId = workflowId;
        PromptTemplateId = promptTemplateId;
        Status = AiAppStatus.Draft;
        PublishVersion = 0;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string? Icon { get; private set; }
    public long? AgentId { get; private set; }
    public long? WorkflowId { get; private set; }
    public long? PromptTemplateId { get; private set; }
    public AiAppStatus Status { get; private set; }
    public int PublishVersion { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }

    public void Update(
        string name,
        string? description,
        string? icon,
        long? agentId,
        long? workflowId,
        long? promptTemplateId)
    {
        Name = name;
        Description = description ?? string.Empty;
        Icon = icon ?? string.Empty;
        AgentId = agentId;
        WorkflowId = workflowId;
        PromptTemplateId = promptTemplateId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Publish()
    {
        Status = AiAppStatus.Published;
        PublishVersion++;
        PublishedAt = DateTime.UtcNow;
        UpdatedAt = PublishedAt;
    }
}

public enum AiAppStatus
{
    Draft = 0,
    Published = 1
}

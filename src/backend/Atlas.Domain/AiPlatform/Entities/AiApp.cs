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
        UiBuilderSchemaJson = "{}";
        WorkspaceLayoutJson = "{}";
        PublishedConnectorConfigJson = "{}";
        LastPublishedSnapshotJson = "{}";
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
        PrimaryWorkflowId = workflowId;
        PromptTemplateId = promptTemplateId;
        UiBuilderSchemaJson = "{}";
        WorkspaceLayoutJson = "{}";
        PublishedConnectorConfigJson = "{}";
        LastPublishedSnapshotJson = "{}";
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
    public long? PrimaryWorkflowId { get; private set; }
    public long? EntryConversationTemplateId { get; private set; }
    public long? PromptTemplateId { get; private set; }
    public string UiBuilderSchemaJson { get; private set; }
    public string WorkspaceLayoutJson { get; private set; }
    public string PublishedConnectorConfigJson { get; private set; }
    public string LastPublishedSnapshotJson { get; private set; }
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
        long? promptTemplateId,
        long? primaryWorkflowId = null,
        long? entryConversationTemplateId = null,
        string? uiBuilderSchemaJson = null,
        string? workspaceLayoutJson = null,
        string? publishedConnectorConfigJson = null,
        string? lastPublishedSnapshotJson = null)
    {
        Name = name;
        Description = description ?? string.Empty;
        Icon = icon ?? string.Empty;
        AgentId = agentId;
        WorkflowId = workflowId;
        PrimaryWorkflowId = primaryWorkflowId ?? workflowId;
        EntryConversationTemplateId = entryConversationTemplateId;
        PromptTemplateId = promptTemplateId;
        UiBuilderSchemaJson = string.IsNullOrWhiteSpace(uiBuilderSchemaJson) ? "{}" : uiBuilderSchemaJson;
        WorkspaceLayoutJson = string.IsNullOrWhiteSpace(workspaceLayoutJson) ? "{}" : workspaceLayoutJson;
        PublishedConnectorConfigJson = string.IsNullOrWhiteSpace(publishedConnectorConfigJson) ? "{}" : publishedConnectorConfigJson;
        LastPublishedSnapshotJson = string.IsNullOrWhiteSpace(lastPublishedSnapshotJson) ? "{}" : lastPublishedSnapshotJson;
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

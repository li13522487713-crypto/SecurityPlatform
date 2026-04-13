using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AiAppResourceBinding : TenantEntity
{
    public AiAppResourceBinding() : base(TenantId.Empty)
    {
        ResourceType = string.Empty;
        Role = string.Empty;
        ConfigJson = "{}";
    }

    public long AppId { get; private set; }
    public string ResourceType { get; private set; }
    public long ResourceId { get; private set; }
    public string Role { get; private set; }
    public int DisplayOrder { get; private set; }
    public string ConfigJson { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
}

public sealed class AiAppConversationTemplate : TenantEntity
{
    public AiAppConversationTemplate() : base(TenantId.Empty)
    {
        Name = string.Empty;
        CreateMethod = AiAppConversationTemplateCreateMethod.Manual;
        ConfigJson = "{}";
    }

    public AiAppConversationTemplate(
        TenantId tenantId,
        long appId,
        string name,
        AiAppConversationTemplateCreateMethod createMethod,
        long? sourceWorkflowId,
        long? connectorId,
        bool isDefault,
        string? configJson,
        long id)
        : base(tenantId)
    {
        Id = id;
        AppId = appId;
        Name = name;
        CreateMethod = createMethod;
        SourceWorkflowId = sourceWorkflowId;
        ConnectorId = connectorId;
        IsDefault = isDefault;
        Version = 1;
        PublishedVersion = 0;
        ConfigJson = string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public long AppId { get; private set; }
    public string Name { get; private set; }
    public AiAppConversationTemplateCreateMethod CreateMethod { get; private set; }
    public long? SourceWorkflowId { get; private set; }
    public long? ConnectorId { get; private set; }
    public bool IsDefault { get; private set; }
    public int Version { get; private set; }
    public int PublishedVersion { get; private set; }
    public string ConfigJson { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public void Update(
        string name,
        long? sourceWorkflowId,
        long? connectorId,
        bool? isDefault,
        string? configJson)
    {
        Name = name;
        SourceWorkflowId = sourceWorkflowId;
        ConnectorId = connectorId;
        if (isDefault.HasValue)
        {
            IsDefault = isDefault.Value;
        }

        if (!string.IsNullOrWhiteSpace(configJson))
        {
            ConfigJson = configJson;
        }

        Version++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkPublished()
    {
        PublishedVersion = Version;
        UpdatedAt = DateTime.UtcNow;
    }
}

public enum AiAppConversationTemplateCreateMethod
{
    Manual = 0,
    NodeGenerated = 1
}

public sealed class AiAppConnectorBinding : TenantEntity
{
    public AiAppConnectorBinding() : base(TenantId.Empty)
    {
        ConnectorKey = string.Empty;
        ConfigJson = "{}";
        Status = string.Empty;
    }

    public long AppId { get; private set; }
    public string ConnectorKey { get; private set; }
    public int PublishVersion { get; private set; }
    public string ConfigJson { get; private set; }
    public string Status { get; private set; }
    public DateTime? LastPublishedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
}

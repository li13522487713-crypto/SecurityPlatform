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

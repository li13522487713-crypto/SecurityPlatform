using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class WorkflowReference : TenantEntity
{
    public WorkflowReference() : base(TenantId.Empty)
    {
        ResourceType = string.Empty;
        PortKey = string.Empty;
        NodeKey = string.Empty;
    }

    public long WorkflowId { get; private set; }
    public string ResourceType { get; private set; }
    public long ResourceId { get; private set; }
    public string PortKey { get; private set; }
    public string NodeKey { get; private set; }
    public DateTime CreatedAt { get; private set; }
}

public sealed class WorkflowPublishedReference : TenantEntity
{
    public WorkflowPublishedReference() : base(TenantId.Empty)
    {
        ResourceType = string.Empty;
        SnapshotJson = "{}";
    }

    public long WorkflowVersionId { get; private set; }
    public string ResourceType { get; private set; }
    public long ResourceId { get; private set; }
    public string SnapshotJson { get; private set; }
    public DateTime CreatedAt { get; private set; }
}

public sealed class WorkflowConversationTemplateLink : TenantEntity
{
    public WorkflowConversationTemplateLink() : base(TenantId.Empty)
    {
    }

    public long WorkflowId { get; private set; }
    public long ConversationTemplateId { get; private set; }
    public DateTime CreatedAt { get; private set; }
}

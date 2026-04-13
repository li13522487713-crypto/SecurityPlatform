using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AgentWorkflowBinding : TenantEntity
{
    public AgentWorkflowBinding() : base(TenantId.Empty)
    {
        BindingRole = AgentWorkflowBindingRole.Default;
        ConfigJson = "{}";
    }

    public long AgentId { get; private set; }
    public long WorkflowId { get; private set; }
    public AgentWorkflowBindingRole BindingRole { get; private set; }
    public int DisplayOrder { get; private set; }
    public string ConfigJson { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
}

public enum AgentWorkflowBindingRole
{
    Default = 0,
    Skill = 1,
    Chatflow = 2
}

public sealed class AgentDatabaseBinding : TenantEntity
{
    public AgentDatabaseBinding() : base(TenantId.Empty)
    {
        ConfigJson = "{}";
    }

    public long AgentId { get; private set; }
    public long DatabaseId { get; private set; }
    public bool PromptDisabled { get; private set; }
    public int DisplayOrder { get; private set; }
    public string ConfigJson { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
}

public sealed class AgentVariableBinding : TenantEntity
{
    public AgentVariableBinding() : base(TenantId.Empty)
    {
        ConfigJson = "{}";
    }

    public long AgentId { get; private set; }
    public long VariableId { get; private set; }
    public int DisplayOrder { get; private set; }
    public string ConfigJson { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
}

public sealed class AgentPromptBinding : TenantEntity
{
    public AgentPromptBinding() : base(TenantId.Empty)
    {
        Version = string.Empty;
        ConfigJson = "{}";
    }

    public long AgentId { get; private set; }
    public long PromptTemplateId { get; private set; }
    public string Version { get; private set; }
    public string ConfigJson { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
}

public sealed class AgentConversationProfile : TenantEntity
{
    public AgentConversationProfile() : base(TenantId.Empty)
    {
        ProfileKey = string.Empty;
        ProfileJson = "{}";
    }

    public long AgentId { get; private set; }
    public string ProfileKey { get; private set; }
    public string ProfileJson { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
}

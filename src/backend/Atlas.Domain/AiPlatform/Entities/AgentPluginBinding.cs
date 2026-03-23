using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AgentPluginBinding : TenantEntity
{
    public AgentPluginBinding()
        : base(TenantId.Empty)
    {
        ToolConfigJson = "{}";
        CreatedAt = DateTime.UtcNow;
    }

    public AgentPluginBinding(
        TenantId tenantId,
        long agentId,
        long pluginId,
        int sortOrder,
        bool isEnabled,
        string? toolConfigJson,
        long id)
        : base(tenantId)
    {
        Id = id;
        AgentId = agentId;
        PluginId = pluginId;
        SortOrder = sortOrder;
        IsEnabled = isEnabled;
        ToolConfigJson = string.IsNullOrWhiteSpace(toolConfigJson) ? "{}" : toolConfigJson;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public long AgentId { get; private set; }
    public long PluginId { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsEnabled { get; private set; }
    public string ToolConfigJson { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public void Update(int sortOrder, bool isEnabled, string? toolConfigJson)
    {
        SortOrder = sortOrder;
        IsEnabled = isEnabled;
        ToolConfigJson = string.IsNullOrWhiteSpace(toolConfigJson) ? "{}" : toolConfigJson;
        UpdatedAt = DateTime.UtcNow;
    }
}

using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities;

/// <summary>
/// 治理 M-G10-C1（S16）：Agent 触发器（定时 / webhook / 事件）。
/// </summary>
[SugarTable("AgentTrigger")]
public sealed class AgentTrigger : TenantEntity
{
    public AgentTrigger()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        TriggerType = "schedule";
        ConfigJson = "{}";
        IsEnabled = false;
        CreatedAt = DateTime.UtcNow;
    }

    public AgentTrigger(
        TenantId tenantId,
        long agentId,
        string name,
        string triggerType,
        string configJson,
        bool isEnabled,
        long createdBy,
        long id)
        : base(tenantId)
    {
        Id = id;
        AgentId = agentId;
        Name = name.Trim();
        TriggerType = triggerType.Trim().ToLowerInvariant();
        ConfigJson = string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson;
        IsEnabled = isEnabled;
        CreatedBy = createdBy;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public long AgentId { get; private set; }

    [SugarColumn(Length = 128, IsNullable = false)]
    public string Name { get; private set; }

    [SugarColumn(Length = 32, IsNullable = false)]
    public string TriggerType { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = false)]
    public string ConfigJson { get; private set; }

    public bool IsEnabled { get; private set; }

    public long CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Update(string name, string triggerType, string configJson, bool isEnabled)
    {
        Name = name.Trim();
        TriggerType = triggerType.Trim().ToLowerInvariant();
        ConfigJson = string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson;
        IsEnabled = isEnabled;
        UpdatedAt = DateTime.UtcNow;
    }
}

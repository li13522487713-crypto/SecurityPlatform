using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LogicFlow.Flows;

[SugarTable("lf_logic_flow_definition")]
public sealed class LogicFlowDefinition : TenantEntity
{
    public LogicFlowDefinition() : base(default) { }

    public LogicFlowDefinition(TenantId tenantId, string name, string displayName, FlowTriggerType triggerType)
        : base(tenantId)
    {
        Name = name;
        DisplayName = displayName;
        TriggerType = triggerType;
        Status = FlowStatus.Draft;
        Version = "1.0.0";
        MaxRetries = 3;
        TimeoutSeconds = 300;
        IsEnabled = true;
        TriggerConfigJson = "{}";
        InputSchemaJson = "{}";
        OutputSchemaJson = "{}";
        CreatedAt = DateTime.UtcNow;
    }

    [SugarColumn(IsPrimaryKey = true)]
    public new long Id { get => base.Id; set => SetId(value); }

    [SugarColumn(Length = 100)]
    public string Name { get; set; } = string.Empty;

    [SugarColumn(Length = 200)]
    public string DisplayName { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? Description { get; set; }

    [SugarColumn(Length = 50)]
    public string Version { get; set; } = "1.0.0";

    public FlowStatus Status { get; set; }

    public FlowTriggerType TriggerType { get; set; }

    [SugarColumn(ColumnDataType = "text")]
    public string TriggerConfigJson { get; set; } = "{}";

    [SugarColumn(ColumnDataType = "text")]
    public string InputSchemaJson { get; set; } = "{}";

    [SugarColumn(ColumnDataType = "text")]
    public string OutputSchemaJson { get; set; } = "{}";

    public int MaxRetries { get; set; } = 3;

    public int TimeoutSeconds { get; set; } = 300;

    public bool IsEnabled { get; set; } = true;

    [SugarColumn(IsNullable = true)]
    public long? SnapshotId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    [SugarColumn(Length = 100)]
    public string CreatedBy { get; set; } = string.Empty;

    [SugarColumn(Length = 100)]
    public string UpdatedBy { get; set; } = string.Empty;

    public void Publish()
    {
        Status = FlowStatus.Published;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        Status = FlowStatus.Archived;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Disable()
    {
        Status = FlowStatus.Disabled;
        IsEnabled = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateDefinition(
        string name,
        string displayName,
        string? description,
        string version,
        FlowTriggerType triggerType,
        string triggerConfigJson,
        string inputSchemaJson,
        string outputSchemaJson,
        int maxRetries,
        int timeoutSeconds,
        bool isEnabled,
        long? snapshotId,
        string updatedBy)
    {
        Name = name;
        DisplayName = displayName;
        Description = description;
        Version = version;
        TriggerType = triggerType;
        TriggerConfigJson = triggerConfigJson;
        InputSchemaJson = inputSchemaJson;
        OutputSchemaJson = outputSchemaJson;
        MaxRetries = maxRetries;
        TimeoutSeconds = timeoutSeconds;
        IsEnabled = isEnabled;
        SnapshotId = snapshotId;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }
}

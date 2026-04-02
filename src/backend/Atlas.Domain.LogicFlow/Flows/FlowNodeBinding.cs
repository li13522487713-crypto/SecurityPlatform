using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LogicFlow.Flows;

[SugarTable("lf_flow_node_binding")]
public sealed class FlowNodeBinding : TenantEntity
{
    public FlowNodeBinding() : base(default) { }

    public FlowNodeBinding(
        TenantId tenantId,
        long flowDefinitionId,
        string nodeTypeKey,
        string nodeInstanceKey,
        string displayName,
        double positionX,
        double positionY,
        int sortOrder)
        : base(tenantId)
    {
        FlowDefinitionId = flowDefinitionId;
        NodeTypeKey = nodeTypeKey;
        NodeInstanceKey = nodeInstanceKey;
        DisplayName = displayName;
        PositionX = positionX;
        PositionY = positionY;
        SortOrder = sortOrder;
        IsEnabled = true;
        ConfigJson = "{}";
        CreatedAt = DateTime.UtcNow;
    }

    [SugarColumn(IsPrimaryKey = true)]
    public new long Id { get => base.Id; set => SetId(value); }

    public long FlowDefinitionId { get; set; }

    [SugarColumn(Length = 100)]
    public string NodeTypeKey { get; set; } = string.Empty;

    [SugarColumn(Length = 100)]
    public string NodeInstanceKey { get; set; } = string.Empty;

    [SugarColumn(Length = 200)]
    public string DisplayName { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "text")]
    public string ConfigJson { get; set; } = "{}";

    public double PositionX { get; set; }

    public double PositionY { get; set; }

    public int SortOrder { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}

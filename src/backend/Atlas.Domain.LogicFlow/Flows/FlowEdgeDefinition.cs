using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LogicFlow.Flows;

[SugarTable("lf_flow_edge_definition")]
public sealed class FlowEdgeDefinition : TenantEntity
{
    public FlowEdgeDefinition() : base(default) { }

    public FlowEdgeDefinition(
        TenantId tenantId,
        long flowDefinitionId,
        string sourceNodeKey,
        string sourcePortKey,
        string targetNodeKey,
        string targetPortKey,
        int priority = 0)
        : base(tenantId)
    {
        FlowDefinitionId = flowDefinitionId;
        SourceNodeKey = sourceNodeKey;
        SourcePortKey = sourcePortKey;
        TargetNodeKey = targetNodeKey;
        TargetPortKey = targetPortKey;
        Priority = priority;
        CreatedAt = DateTime.UtcNow;
    }

    [SugarColumn(IsPrimaryKey = true)]
    public new long Id { get => base.Id; set => SetId(value); }

    public long FlowDefinitionId { get; set; }

    [SugarColumn(Length = 100)]
    public string SourceNodeKey { get; set; } = string.Empty;

    [SugarColumn(Length = 100)]
    public string SourcePortKey { get; set; } = string.Empty;

    [SugarColumn(Length = 100)]
    public string TargetNodeKey { get; set; } = string.Empty;

    [SugarColumn(Length = 100)]
    public string TargetPortKey { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ConditionExpression { get; set; }

    public int Priority { get; set; }

    [SugarColumn(Length = 200, IsNullable = true)]
    public string? Label { get; set; }

    [SugarColumn(Length = 50, IsNullable = true)]
    public string? EdgeStyle { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

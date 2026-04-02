using Atlas.Domain.LogicFlow.Flows;

namespace Atlas.Application.LogicFlow.Flows.Models;

public class LogicFlowCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? Version { get; set; }
    public int TriggerType { get; set; }
    public string? TriggerConfigJson { get; set; }
    public string? InputSchemaJson { get; set; }
    public string? OutputSchemaJson { get; set; }
    public int? MaxRetries { get; set; }
    public int? TimeoutSeconds { get; set; }
}

public sealed class LogicFlowUpdateRequest : LogicFlowCreateRequest
{
    public required bool IsEnabled { get; set; }
}

public class LogicFlowResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Version { get; set; } = string.Empty;
    public FlowStatus Status { get; set; }
    public int TriggerType { get; set; }
    public string TriggerConfigJson { get; set; } = "{}";
    public string InputSchemaJson { get; set; } = "{}";
    public string OutputSchemaJson { get; set; } = "{}";
    public int MaxRetries { get; set; }
    public int TimeoutSeconds { get; set; }
    public bool IsEnabled { get; set; }
    public string? SnapshotId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string UpdatedBy { get; set; } = string.Empty;
}

public sealed class LogicFlowListItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public FlowStatus Status { get; set; }
    public int TriggerType { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class FlowNodeBindingRequest
{
    public string NodeTypeKey { get; set; } = string.Empty;
    public string NodeInstanceKey { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? ConfigJson { get; set; }
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public int? SortOrder { get; set; }
}

public sealed class FlowEdgeRequest
{
    public string SourceNodeKey { get; set; } = string.Empty;
    public string SourcePortKey { get; set; } = string.Empty;
    public string TargetNodeKey { get; set; } = string.Empty;
    public string TargetPortKey { get; set; } = string.Empty;
    public string? ConditionExpression { get; set; }
    public int? Priority { get; set; }
    public string? Label { get; set; }
    public string? EdgeStyle { get; set; }
}

public sealed class FlowNodeBindingResponse
{
    public string Id { get; set; } = string.Empty;
    public string FlowDefinitionId { get; set; } = string.Empty;
    public string NodeTypeKey { get; set; } = string.Empty;
    public string NodeInstanceKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = "{}";
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; }
}

public sealed class FlowEdgeResponse
{
    public string Id { get; set; } = string.Empty;
    public string FlowDefinitionId { get; set; } = string.Empty;
    public string SourceNodeKey { get; set; } = string.Empty;
    public string SourcePortKey { get; set; } = string.Empty;
    public string TargetNodeKey { get; set; } = string.Empty;
    public string TargetPortKey { get; set; } = string.Empty;
    public string? ConditionExpression { get; set; }
    public int Priority { get; set; }
    public string? Label { get; set; }
    public string? EdgeStyle { get; set; }
}

public sealed class LogicFlowDetailResponse : LogicFlowResponse
{
    public List<FlowNodeBindingResponse> Nodes { get; set; } = [];
    public List<FlowEdgeResponse> Edges { get; set; } = [];
}

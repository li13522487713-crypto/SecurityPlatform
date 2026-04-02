using Atlas.Domain.LogicFlow.Flows;

namespace Atlas.Application.LogicFlow.Flows.Models;

public sealed class FlowExecutionTriggerRequest
{
    public long FlowDefinitionId { get; set; }
    public string? InputDataJson { get; set; }
    public string? CorrelationId { get; set; }
}

public sealed class FlowExecutionResponse
{
    public string Id { get; set; } = string.Empty;
    public long FlowDefinitionId { get; set; }
    public string Version { get; set; } = string.Empty;
    public ExecutionStatus Status { get; set; }
    public FlowTriggerType TriggerType { get; set; }
    public string InputDataJson { get; set; } = "{}";
    public string OutputDataJson { get; set; } = "{}";
    public string? ErrorMessage { get; set; }
    public string? StartedAt { get; set; }
    public string? CompletedAt { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public long? DurationMs { get; set; }
    public string? CurrentNodeKey { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; }
    public string? SnapshotId { get; set; }
    public string? CorrelationId { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? ParentExecutionId { get; set; }
}

public sealed class FlowExecutionListItem
{
    public string Id { get; set; } = string.Empty;
    public long FlowDefinitionId { get; set; }
    public string Version { get; set; } = string.Empty;
    public ExecutionStatus Status { get; set; }
    public FlowTriggerType TriggerType { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long? DurationMs { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public sealed class NodeRunResponse
{
    public string Id { get; set; } = string.Empty;
    public long FlowExecutionId { get; set; }
    public string NodeKey { get; set; } = string.Empty;
    public string NodeTypeKey { get; set; } = string.Empty;
    public NodeRunStatus Status { get; set; }
    public string InputDataJson { get; set; } = "{}";
    public string OutputDataJson { get; set; } = "{}";
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; }
    public string? StartedAt { get; set; }
    public string? CompletedAt { get; set; }
    public long? DurationMs { get; set; }
    public string? CompensationDataJson { get; set; }
    public bool IsCompensated { get; set; }
}

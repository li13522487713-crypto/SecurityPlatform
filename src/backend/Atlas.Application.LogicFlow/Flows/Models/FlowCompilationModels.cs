namespace Atlas.Application.LogicFlow.Flows.Models;

public sealed class FlowValidationError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? NodeKey { get; set; }
}

public sealed class FlowValidationResult
{
    public bool IsValid { get; set; }
    public List<FlowValidationError> Errors { get; set; } = [];
}

public sealed class PhysicalDagNode
{
    public string NodeKey { get; set; } = string.Empty;
    public string TypeKey { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = "{}";
    public List<string> Dependencies { get; set; } = [];
}

public sealed class PhysicalDagEdge
{
    public string SourceNodeKey { get; set; } = string.Empty;
    public string SourcePortKey { get; set; } = string.Empty;
    public string TargetNodeKey { get; set; } = string.Empty;
    public string TargetPortKey { get; set; } = string.Empty;
}

public sealed class PhysicalDagPlan
{
    public long FlowId { get; set; }
    public List<PhysicalDagNode> Nodes { get; set; } = [];
    public List<PhysicalDagEdge> Edges { get; set; } = [];
}

public sealed class NodeExecutionRequest
{
    public long FlowExecutionId { get; set; }
    public string NodeKey { get; set; } = string.Empty;
    public string TypeKey { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = "{}";
    public Dictionary<string, string> InputData { get; set; } = new(StringComparer.Ordinal);
    public int RetryAttempt { get; set; }
    public int MaxRetries { get; set; } = 3;
    public int TimeoutSeconds { get; set; }
}

public sealed class NodeExecutionResult
{
    public string NodeKey { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public Dictionary<string, string> OutputData { get; set; } = new(StringComparer.Ordinal);
    public string? ErrorMessage { get; set; }
    public long DurationMs { get; set; }
}

using Atlas.Core.Models;

namespace Atlas.Application.Visualization.Models;

public sealed record VisualizationFilterRequest
{
    public string? Department { get; init; }
    public string? FlowType { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
}

public sealed record VisualizationOverviewResponse(
    int TotalProcesses,
    int RunningInstances,
    int BlockedNodes,
    int AlertsToday,
    IReadOnlyList<string> RiskHints);

public sealed record VisualizationProcessSummary
{
    public string Id { get; init; } = default!;
    public string Name { get; init; } = default!;
    public int Version { get; init; }
    public string Status { get; init; } = "Draft";
    public DateTimeOffset? PublishedAt { get; init; }
}

public sealed record VisualizationProcessDetail
{
    public string Id { get; init; } = default!;
    public string Name { get; init; } = default!;
    public int Version { get; init; }
    public string Status { get; init; } = "Draft";
    public DateTimeOffset? PublishedAt { get; init; }
    public string DefinitionJson { get; init; } = "{}";
}

public sealed record VisualizationInstanceSummary
{
    public string Id { get; init; } = default!;
    public string FlowName { get; init; } = default!;
    public string Status { get; init; } = "Running";
    public string CurrentNode { get; init; } = default!;
    public DateTimeOffset StartedAt { get; init; }
    public int DurationMinutes { get; init; }
}

public sealed record VisualizationInstanceDetail
{
    public string Id { get; init; } = default!;
    public string FlowName { get; init; } = default!;
    public string Status { get; init; } = "Running";
    public string CurrentNode { get; init; } = default!;
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
    public IReadOnlyList<NodeTrace> Trace { get; init; } = Array.Empty<NodeTrace>();
    public IReadOnlyList<string> RiskHints { get; init; } = Array.Empty<string>();
}

public sealed record NodeTrace
{
    public string NodeId { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string Status { get; init; } = "Completed";
    public int DurationMinutes { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? EndedAt { get; init; }
}

public sealed record ValidateVisualizationRequest
{
    public string DefinitionJson { get; init; } = "{}";
}

public sealed record PublishVisualizationRequest
{
    public string ProcessId { get; init; } = default!;
    public int Version { get; init; } = 1;
    public string? Note { get; init; }
}

public sealed record SaveVisualizationProcessRequest
{
    public string? ProcessId { get; init; }
    public string Name { get; init; } = default!;
    public string DefinitionJson { get; init; } = "{}";
}

public sealed record VisualizationValidationResponse(
    bool Passed,
    IReadOnlyList<string> Errors);

public sealed record VisualizationPublishResponse(
    string ProcessId,
    int Version,
    string Status);

public sealed record SaveVisualizationProcessResponse(
    string ProcessId,
    int Version,
    string Status);

public sealed record VisualizationMetricsResponse(
    int TotalProcesses,
    int DraftProcesses,
    int RunningInstances,
    int CompletedInstances,
    int PendingTasks,
    int OverdueTasks,
    int AssetsTotal,
    int AlertsToday,
    int AuditEventsToday);

#region X6 画布定义（强类型约束）

public sealed record CanvasDefinition
{
    public List<CanvasCell> Cells { get; init; } = new();
}

public sealed record CanvasCell
{
    public string Id { get; init; } = default!;
    public string Shape { get; init; } = default!;
    public CanvasData Data { get; init; } = new();
    public CanvasEdge? Source { get; init; }
    public CanvasEdge? Target { get; init; }
}

public sealed record CanvasData
{
    public string? Type { get; init; }
    public string? Name { get; init; }
    public string? Assignee { get; init; }
    public int? TimeoutMinutes { get; init; }
}

public sealed record CanvasEdge
{
    public string? Cell { get; init; }
}

#endregion

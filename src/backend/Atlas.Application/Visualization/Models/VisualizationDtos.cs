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

public sealed record VisualizationInstanceSummary
{
    public string Id { get; init; } = default!;
    public string FlowName { get; init; } = default!;
    public string Status { get; init; } = "Running";
    public string CurrentNode { get; init; } = default!;
    public DateTimeOffset StartedAt { get; init; }
    public int DurationMinutes { get; init; }
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

public sealed record VisualizationValidationResponse(
    bool Passed,
    IReadOnlyList<string> Errors);

public sealed record VisualizationPublishResponse(
    string ProcessId,
    int Version,
    string Status);

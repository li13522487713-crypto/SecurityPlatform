namespace Atlas.Application.AiPlatform.Models;

public sealed record WorkflowWorkbenchExecuteRequest(
    string Incident,
    string? Source = null);

public sealed record WorkflowWorkbenchExecutionDto(
    string ExecutionId,
    string? Status,
    string? OutputsJson,
    string? ErrorMessage);

public sealed record WorkflowWorkbenchTraceStepDto(
    string NodeKey,
    string? Status,
    string? NodeType,
    long? DurationMs,
    string? ErrorMessage);

public sealed record WorkflowWorkbenchTraceDto(
    string ExecutionId,
    string? Status,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    long? DurationMs,
    IReadOnlyList<WorkflowWorkbenchTraceStepDto> Steps);

public sealed record WorkflowWorkbenchExecuteResultDto(
    WorkflowWorkbenchExecutionDto Execution,
    WorkflowWorkbenchTraceDto? Trace);

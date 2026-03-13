using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Application.AiPlatform.Models;

public sealed record AiWorkflowDefinitionDto(
    long Id,
    string Name,
    string? Description,
    AiWorkflowStatus Status,
    int PublishVersion,
    long CreatorId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? PublishedAt);

public sealed record AiWorkflowDetailDto(
    long Id,
    string Name,
    string? Description,
    string CanvasJson,
    string DefinitionJson,
    AiWorkflowStatus Status,
    int PublishVersion,
    long CreatorId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? PublishedAt);

public sealed record AiWorkflowCreateRequest(
    string Name,
    string? Description,
    string CanvasJson,
    string DefinitionJson);

public sealed record AiWorkflowSaveRequest(
    string CanvasJson,
    string DefinitionJson);

public sealed record AiWorkflowMetaUpdateRequest(
    string Name,
    string? Description);

public sealed record AiWorkflowValidateResult(
    bool IsValid,
    IReadOnlyList<string> Errors);

public sealed record AiWorkflowExecutionRunRequest(
    Dictionary<string, object?> Inputs);

public sealed record AiWorkflowExecutionRunResult(string ExecutionId);

public sealed record AiWorkflowExecutionProgressDto(
    string ExecutionId,
    string WorkflowId,
    int Version,
    string Status,
    DateTime CreatedAt,
    DateTime? CompletedAt);

public sealed record AiWorkflowNodeHistoryItem(
    string PointerId,
    int StepId,
    string? StepName,
    string Status,
    DateTime? StartTime,
    DateTime? EndTime,
    object? Outcome);

public sealed record AiWorkflowNodeTypeDto(
    string Key,
    string Name,
    string Category,
    string Description);

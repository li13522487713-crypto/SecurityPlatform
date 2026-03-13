using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Application.AiPlatform.Models;

// ── 请求模型 ──────────────────────────────────────────────

public sealed record WorkflowV2CreateRequest(
    string Name,
    string? Description,
    WorkflowMode Mode);

public sealed record WorkflowV2SaveDraftRequest(
    string CanvasJson,
    string? CommitId);

public sealed record WorkflowV2UpdateMetaRequest(
    string Name,
    string? Description);

public sealed record WorkflowV2PublishRequest(
    string? ChangeLog);

public sealed record WorkflowV2RunRequest(
    string? InputsJson);

public sealed record WorkflowV2NodeDebugRequest(
    string NodeKey,
    string? InputsJson);

// ── 响应模型 ──────────────────────────────────────────────

public sealed record WorkflowV2ListItem(
    long Id,
    string Name,
    string? Description,
    WorkflowMode Mode,
    WorkflowLifecycleStatus Status,
    int LatestVersionNumber,
    long CreatorId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? PublishedAt);

public sealed record WorkflowV2DetailDto(
    long Id,
    string Name,
    string? Description,
    WorkflowMode Mode,
    WorkflowLifecycleStatus Status,
    int LatestVersionNumber,
    long CreatorId,
    string CanvasJson,
    string? CommitId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? PublishedAt);

public sealed record WorkflowV2VersionDto(
    long Id,
    long WorkflowId,
    int VersionNumber,
    string? ChangeLog,
    string CanvasJson,
    DateTime PublishedAt,
    long PublishedByUserId);

public sealed record WorkflowV2ExecutionDto(
    long Id,
    long WorkflowId,
    int VersionNumber,
    ExecutionStatus Status,
    string? InputsJson,
    string? OutputsJson,
    string? ErrorMessage,
    DateTime StartedAt,
    DateTime? CompletedAt,
    IReadOnlyList<WorkflowV2NodeExecutionDto> NodeExecutions);

public sealed record WorkflowV2NodeExecutionDto(
    long Id,
    long ExecutionId,
    string NodeKey,
    WorkflowNodeType NodeType,
    ExecutionStatus Status,
    string? InputsJson,
    string? OutputsJson,
    string? ErrorMessage,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    long? DurationMs);

public sealed record WorkflowV2RunResult(string ExecutionId);

public sealed record WorkflowV2NodeTypeDto(
    string Key,
    string Name,
    string Category,
    string Description);

/// <summary>
/// SSE 流式事件封装。
/// </summary>
public sealed record SseEvent(string Event, string Data);

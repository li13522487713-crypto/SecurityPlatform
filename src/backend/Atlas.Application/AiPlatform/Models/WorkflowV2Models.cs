using System.Text.Json;
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

public sealed record WorkflowV2ExecutionCheckpointDto(
    long ExecutionId,
    long WorkflowId,
    ExecutionStatus Status,
    string? LastNodeKey,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string? InputsJson,
    string? OutputsJson,
    string? ErrorMessage);

public sealed record WorkflowV2ExecutionDebugViewDto(
    WorkflowV2ExecutionDto Execution,
    WorkflowV2NodeExecutionDto? FocusNode,
    string FocusReason);

public sealed record WorkflowV2RunResult(
    string ExecutionId,
    ExecutionStatus? Status = null,
    string? OutputsJson = null,
    string? ErrorMessage = null,
    string? DebugNodeKey = null);

public sealed record WorkflowV2NodeTypeDto(
    string Key,
    string Name,
    string Category,
    string Description,
    IReadOnlyList<WorkflowNodePortMetadata>? Ports = null,
    string? ConfigSchemaJson = null,
    WorkflowNodeUiMetadata? UiMeta = null);

public sealed record WorkflowV2NodeTemplateDto(
    string Key,
    string Name,
    string Category,
    Dictionary<string, JsonElement> DefaultConfig);

/// <summary>
/// SSE 流式事件封装。
/// </summary>
public sealed record SseEvent(string Event, string Data);

/// <summary>
/// 两个工作流版本之间的 Diff 结果。
/// </summary>
public sealed record WorkflowVersionDiff(
    long WorkflowId,
    long FromVersionId,
    int FromVersionNumber,
    long ToVersionId,
    int ToVersionNumber,
    IReadOnlyList<string> AddedNodeKeys,
    IReadOnlyList<string> RemovedNodeKeys,
    IReadOnlyList<string> ModifiedNodeKeys,
    int AddedConnectionCount,
    int RemovedConnectionCount,
    bool HasChanges);

public sealed record WorkflowVersionRollbackResult(
    long WorkflowId,
    long RolledBackToVersionId,
    int NewVersionNumber);

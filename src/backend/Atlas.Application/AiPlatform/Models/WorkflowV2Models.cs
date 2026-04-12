using System.Text.Json;
using System.Text.Json.Serialization;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Domain.AiPlatform.ValueObjects;

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

public sealed record WorkflowV2RunRequest
{
    public WorkflowV2RunRequest()
    {
    }

    public WorkflowV2RunRequest(string? inputsJson, string? source = null)
    {
        InputsJson = inputsJson;
        Source = source;
    }

    [JsonPropertyName("inputsJson")]
    public string? InputsJson { get; init; }

    [JsonPropertyName("source")]
    public string? Source { get; init; }
}

public sealed record WorkflowV2ResumeRequest(
    string? InputsJson,
    Dictionary<string, JsonElement>? Data,
    Dictionary<string, JsonElement>? VariableOverrides = null);

public sealed record WorkflowV2NodeDebugRequest(
    string NodeKey,
    string? InputsJson,
    string? Source = null,
    long? VersionId = null);

public sealed record WorkflowV2ValidateRequest(
    string? CanvasJson = null,
    CanvasSchema? Canvas = null);

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
    string? DebugNodeKey = null,
    WorkflowV2StepResultDto? StepResult = null);

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

public sealed record WorkflowV2StepResultDto(
    string ExecutionId,
    string NodeKey,
    WorkflowNodeType NodeType,
    ExecutionStatus Status,
    DateTime? StartedAt = null,
    DateTime? CompletedAt = null,
    long? DurationMs = null,
    Dictionary<string, JsonElement>? Inputs = null,
    Dictionary<string, JsonElement>? Outputs = null,
    string? ErrorMessage = null,
    Dictionary<string, JsonElement>? BranchDecision = null);

public sealed record WorkflowV2EdgeRuntimeStatusDto(
    string SourceNodeKey,
    string SourcePort,
    string TargetNodeKey,
    string TargetPort,
    EdgeExecutionStatus Status,
    string? Reason = null);

public sealed record WorkflowV2RunTraceDto(
    string ExecutionId,
    long? WorkflowId,
    ExecutionStatus Status,
    DateTime? StartedAt = null,
    DateTime? CompletedAt = null,
    long? DurationMs = null,
    IReadOnlyList<WorkflowV2StepResultDto>? Steps = null,
    IReadOnlyList<WorkflowV2EdgeRuntimeStatusDto>? EdgeStatuses = null);

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

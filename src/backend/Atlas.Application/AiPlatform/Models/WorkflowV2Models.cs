using System.Text.Json;
using System.Text.Json.Serialization;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Domain.AiPlatform.ValueObjects;

namespace Atlas.Application.AiPlatform.Models;

// ── 请求模型 ──────────────────────────────────────────────

public sealed record WorkflowV2CreateRequest(
    string Name,
    string? Description,
    WorkflowMode Mode,
    long? WorkspaceId = null);

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

public sealed record WorkflowV2DependencyItemDto(
    string ResourceType,
    string ResourceId,
    string Name,
    string? Description = null,
    IReadOnlyList<string>? SourceNodeKeys = null);

public sealed record WorkflowV2DependencyDto(
    string WorkflowId,
    IReadOnlyList<WorkflowV2DependencyItemDto> SubWorkflows,
    IReadOnlyList<WorkflowV2DependencyItemDto> Plugins,
    IReadOnlyList<WorkflowV2DependencyItemDto> KnowledgeBases,
    IReadOnlyList<WorkflowV2DependencyItemDto> Databases,
    IReadOnlyList<WorkflowV2DependencyItemDto> Variables,
    IReadOnlyList<WorkflowV2DependencyItemDto> Conversations);

/// <summary>
/// 变量来源类别：与 Coze 上游 GlobalVariableKey 体系对齐，
/// 同时为 Atlas 的 Tenant/User/Conversation 范围预留扩展。
/// </summary>
public enum WorkflowVariableScopeKind
{
    /// <summary>来自上游节点输出。</summary>
    Node = 0,
    /// <summary>画布全局变量（CanvasSchema.Globals）。</summary>
    Global = 1,
    /// <summary>系统变量（用户、会话、租户级，由 IAiVariableService 提供）。</summary>
    System = 2,
    /// <summary>会话级变量。</summary>
    Conversation = 3,
    /// <summary>用户级变量。</summary>
    User = 4
}

/// <summary>
/// 变量字段定义（递归结构：Object/Array 通过 Children 描述子字段）。
/// </summary>
public sealed record WorkflowVariableField(
    string Key,
    string Name,
    string DataType,
    string? Description = null,
    bool Required = false,
    string? DefaultValue = null,
    IReadOnlyList<WorkflowVariableField>? Children = null);

/// <summary>
/// 变量树分组：按节点 / 全局 / 系统 / 会话等聚类，便于前端 Tree 渲染。
/// </summary>
public sealed record WorkflowVariableGroup(
    WorkflowVariableScopeKind Scope,
    string GroupKey,
    string GroupName,
    string? SourceNodeKey,
    string? SourceNodeType,
    IReadOnlyList<WorkflowVariableField> Fields,
    string? Description = null);

/// <summary>
/// 变量树聚合：供节点配置面板 / Prompt 编辑器 / JSON 映射器使用。
///
/// 命名约定（M2 之后）：M1 新增的对外 DTO 不再带 V2 后缀，沿用业务正式名称；
/// 存量 <c>WorkflowV2*</c> 命名（控制器、服务接口本身、路径）仍属遗留命名，等 M7+ 统一重命名。
/// </summary>
public sealed record WorkflowVariableTreeDto(
    string WorkflowId,
    string? NodeKey,
    IReadOnlyList<WorkflowVariableGroup> Groups);

/// <summary>
/// 节点执行历史快照（含输入、输出、上下文变量、错误信息），
/// 用于 Coze /api/workflow_api/get_node_execute_history。
/// </summary>
public sealed record WorkflowNodeExecutionHistoryDto(
    string WorkflowId,
    string ExecutionId,
    string NodeKey,
    string NodeType,
    ExecutionStatus Status,
    string? InputJson,
    string? OutputJson,
    string? ContextVariablesJson,
    string? ErrorMessage,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    long? DurationMs);

/// <summary>
/// 历史 schema 快照：返回画布 JSON 与版本元信息。
/// </summary>
public sealed record WorkflowHistorySchemaDto(
    string WorkflowId,
    string? CommitId,
    string SchemaJson,
    string Name,
    string? Description,
    DateTime SnapshotAt);

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

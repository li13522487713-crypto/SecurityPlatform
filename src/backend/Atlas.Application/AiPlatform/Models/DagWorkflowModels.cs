using System.Text.Json;
using System.Text.Json.Serialization;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Domain.AiPlatform.ValueObjects;

namespace Atlas.Application.AiPlatform.Models;

// ── 请求模型 ──────────────────────────────────────────────

public sealed record DagWorkflowCreateRequest(
    string Name,
    string? Description,
    WorkflowMode Mode,
    long? WorkspaceId = null);

public sealed record DagWorkflowSaveDraftRequest(
    string CanvasJson,
    string? CommitId);

public sealed record DagWorkflowUpdateMetaRequest(
    string Name,
    string? Description);

public sealed record DagWorkflowPublishRequest(
    string? ChangeLog);

public sealed record DagWorkflowRunRequest
{
    public DagWorkflowRunRequest()
    {
    }

    public DagWorkflowRunRequest(string? inputsJson, string? source = null)
    {
        InputsJson = inputsJson;
        Source = source;
    }

    [JsonPropertyName("inputsJson")]
    public string? InputsJson { get; init; }

    [JsonPropertyName("source")]
    public string? Source { get; init; }
}

public sealed record DagWorkflowResumeRequest(
    string? InputsJson,
    Dictionary<string, JsonElement>? Data,
    Dictionary<string, JsonElement>? VariableOverrides = null);

public sealed record DagWorkflowNodeDebugRequest(
    string NodeKey,
    string? InputsJson,
    string? Source = null,
    long? VersionId = null);

public sealed record DagWorkflowValidateRequest(
    string? CanvasJson = null,
    CanvasSchema? Canvas = null);

// ── 响应模型 ──────────────────────────────────────────────

public sealed record DagWorkflowListItem(
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

public sealed record DagWorkflowDetailDto(
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

public sealed record DagWorkflowVersionDto(
    long Id,
    long WorkflowId,
    int VersionNumber,
    string? ChangeLog,
    string CanvasJson,
    DateTime PublishedAt,
    long PublishedByUserId);

public sealed record DagWorkflowExecutionDto(
    long Id,
    long WorkflowId,
    int VersionNumber,
    ExecutionStatus Status,
    string? InputsJson,
    string? OutputsJson,
    string? ErrorMessage,
    DateTime StartedAt,
    DateTime? CompletedAt,
    IReadOnlyList<DagWorkflowNodeExecutionDto> NodeExecutions);

public sealed record DagWorkflowNodeExecutionDto(
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

public sealed record DagWorkflowExecutionCheckpointDto(
    long ExecutionId,
    long WorkflowId,
    ExecutionStatus Status,
    string? LastNodeKey,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string? InputsJson,
    string? OutputsJson,
    string? ErrorMessage);

public sealed record DagWorkflowExecutionDebugViewDto(
    DagWorkflowExecutionDto Execution,
    DagWorkflowNodeExecutionDto? FocusNode,
    string FocusReason);

public sealed record DagWorkflowRunResult(
    string ExecutionId,
    ExecutionStatus? Status = null,
    string? OutputsJson = null,
    string? ErrorMessage = null,
    string? DebugNodeKey = null,
    DagWorkflowStepResultDto? StepResult = null);

public sealed record DagWorkflowNodeTypeDto(
    string Key,
    string Name,
    string Category,
    string Description,
    IReadOnlyList<WorkflowNodePortMetadata>? Ports = null,
    string? ConfigSchemaJson = null,
    WorkflowNodeUiMetadata? UiMeta = null);

public sealed record DagWorkflowNodeTemplateDto(
    string Key,
    string Name,
    string Category,
    Dictionary<string, JsonElement> DefaultConfig);

public sealed record DagWorkflowStepResultDto(
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

public sealed record DagWorkflowEdgeRuntimeStatusDto(
    string SourceNodeKey,
    string SourcePort,
    string TargetNodeKey,
    string TargetPort,
    EdgeExecutionStatus Status,
    string? Reason = null);

public sealed record DagWorkflowRunTraceDto(
    string ExecutionId,
    long? WorkflowId,
    ExecutionStatus Status,
    DateTime? StartedAt = null,
    DateTime? CompletedAt = null,
    long? DurationMs = null,
    IReadOnlyList<DagWorkflowStepResultDto>? Steps = null,
    IReadOnlyList<DagWorkflowEdgeRuntimeStatusDto>? EdgeStatuses = null);

public sealed record DagWorkflowDependencyItemDto(
    string ResourceType,
    string ResourceId,
    string Name,
    string? Description = null,
    IReadOnlyList<string>? SourceNodeKeys = null);

public sealed record DagWorkflowDependencyDto(
    string WorkflowId,
    IReadOnlyList<DagWorkflowDependencyItemDto> SubWorkflows,
    IReadOnlyList<DagWorkflowDependencyItemDto> Plugins,
    IReadOnlyList<DagWorkflowDependencyItemDto> KnowledgeBases,
    IReadOnlyList<DagWorkflowDependencyItemDto> Databases,
    IReadOnlyList<DagWorkflowDependencyItemDto> Variables,
    IReadOnlyList<DagWorkflowDependencyItemDto> Conversations);

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
/// DAG 引擎请求/响应 DTO 使用 <c>DagWorkflow*</c> 前缀；HTTP 路径仍为 <c>api/v2/workflows</c>（<c>v2</c> 表示 API 版本号）。
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

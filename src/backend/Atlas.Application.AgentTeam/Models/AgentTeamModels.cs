using Atlas.Domain.AgentTeam.Entities;

namespace Atlas.Application.AgentTeam.Models;

public sealed record AgentTeamCreateRequest(
    string TeamName,
    string? Description,
    string Owner,
    IReadOnlyList<string> Collaborators,
    TeamRiskLevel RiskLevel,
    IReadOnlyList<string> Tags,
    string? DefaultModelPolicyJson,
    string? BudgetPolicyJson,
    string? PermissionScopeJson);

public sealed record AgentTeamUpdateRequest(
    string TeamName,
    string? Description,
    string Owner,
    IReadOnlyList<string> Collaborators,
    TeamRiskLevel RiskLevel,
    IReadOnlyList<string> Tags,
    string? DefaultModelPolicyJson,
    string? BudgetPolicyJson,
    string? PermissionScopeJson);

public sealed record AgentTeamListItem(
    long Id,
    string TeamName,
    string? Description,
    string Owner,
    TeamStatus Status,
    TeamPublishStatus PublishStatus,
    long? PublishedVersionId,
    TeamRiskLevel RiskLevel,
    int Version,
    DateTime UpdatedAt);

public sealed record AgentTeamDetail(
    long Id,
    string TeamName,
    string? Description,
    string Owner,
    IReadOnlyList<string> Collaborators,
    TeamStatus Status,
    TeamPublishStatus PublishStatus,
    long? PublishedVersionId,
    TeamRiskLevel RiskLevel,
    int Version,
    IReadOnlyList<string> Tags,
    string DefaultModelPolicyJson,
    string BudgetPolicyJson,
    string PermissionScopeJson,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record SubAgentCreateRequest(
    string AgentName,
    string Role,
    string Goal,
    string? Boundaries,
    string PromptTemplate,
    string ModelConfigJson,
    string ToolPermissionsJson,
    string KnowledgeScopesJson,
    string InputSchemaJson,
    string OutputSchemaJson,
    string MemoryPolicyJson,
    string TimeoutPolicyJson,
    string RetryPolicyJson,
    string FallbackPolicyJson,
    string VisibilityPolicyJson,
    SubAgentStatus Status);

public sealed record SubAgentUpdateRequest(
    string AgentName,
    string Role,
    string Goal,
    string? Boundaries,
    string PromptTemplate,
    string ModelConfigJson,
    string ToolPermissionsJson,
    string KnowledgeScopesJson,
    string InputSchemaJson,
    string OutputSchemaJson,
    string MemoryPolicyJson,
    string TimeoutPolicyJson,
    string RetryPolicyJson,
    string FallbackPolicyJson,
    string VisibilityPolicyJson,
    SubAgentStatus Status);

public sealed record SubAgentItem(
    long Id,
    long TeamId,
    string AgentName,
    string Role,
    string Goal,
    SubAgentStatus Status,
    DateTime UpdatedAt);

public sealed record OrchestrationNodeCreateRequest(
    string NodeName,
    NodeType NodeType,
    long? BindAgentId,
    IReadOnlyList<long> Dependencies,
    NodeExecutionMode ExecutionMode,
    string? ConditionExpression,
    string InputBindingJson,
    string OutputBindingJson,
    string RetryRuleJson,
    string TimeoutRuleJson,
    bool HumanApprovalRequired,
    long? FallbackNodeId,
    int Priority,
    bool IsCritical,
    bool SkipAllowed);

public sealed record OrchestrationNodeUpdateRequest(
    string NodeName,
    NodeType NodeType,
    long? BindAgentId,
    IReadOnlyList<long> Dependencies,
    NodeExecutionMode ExecutionMode,
    string? ConditionExpression,
    string InputBindingJson,
    string OutputBindingJson,
    string RetryRuleJson,
    string TimeoutRuleJson,
    bool HumanApprovalRequired,
    long? FallbackNodeId,
    int Priority,
    bool IsCritical,
    bool SkipAllowed);

public sealed record OrchestrationNodeItem(
    long Id,
    long TeamId,
    string NodeName,
    NodeType NodeType,
    long? BindAgentId,
    IReadOnlyList<long> Dependencies,
    NodeExecutionMode ExecutionMode,
    bool HumanApprovalRequired,
    bool IsCritical,
    bool SkipAllowed,
    DateTime UpdatedAt);

public sealed record TeamPublishRequest(string? ReleaseNote, bool RequiresApproval, string? ApprovalRecordId);

public sealed record TeamVersionItem(
    long Id,
    long TeamId,
    string VersionNo,
    TeamPublishStatus PublishStatus,
    string? PublishedBy,
    DateTime? PublishedAt,
    long? RollbackFromVersionId);

public sealed record AgentTeamRunCreateRequest(
    long TeamId,
    long TeamVersionId,
    TriggerType TriggerType,
    string InputPayloadJson);

public sealed record AgentTeamRunInterveneRequest(
    string Action,
    string? PayloadJson);

public sealed record AgentTeamRunDetail(
    long Id,
    long TeamId,
    long TeamVersionId,
    RunStatus CurrentState,
    string InputPayloadJson,
    string OutputResultJson,
    string? OutputSummary,
    string ErrorRecordsJson,
    DateTime StartedAt,
    DateTime? EndedAt);

public sealed record NodeRunItem(
    long Id,
    long RunId,
    long NodeId,
    long? AgentId,
    NodeRunStatus State,
    int RetryCount,
    string InputSnapshotJson,
    string OutputSnapshotJson,
    string? ErrorCode,
    string? ErrorMessage,
    DateTime StartedAt,
    DateTime? EndedAt,
    bool HumanInterventionAllowed);

public sealed record AgentTeamDebugRequest(string InputPayloadJson, bool FullChain, long? NodeId, long? SubAgentId);

public sealed record AgentTeamDebugResult(
    bool Success,
    string Message,
    string OutputJson,
    IReadOnlyList<NodeRunItem> NodeRuns);

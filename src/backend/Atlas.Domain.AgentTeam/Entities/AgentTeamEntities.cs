using System.Text.Json.Serialization;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AgentTeam.Entities;

public sealed class AgentTeamDefinition : TenantEntity
{
    public AgentTeamDefinition()
        : base(TenantId.Empty)
    {
        TeamName = string.Empty;
        Description = string.Empty;
        Owner = string.Empty;
        CollaboratorsJson = "[]";
        PublishStatus = TeamPublishStatus.Unpublished;
        DefaultModelPolicyJson = "{}";
        BudgetPolicyJson = "{}";
        PermissionScopeJson = "{}";
        RiskLevel = TeamRiskLevel.Low;
        TagsJson = "[]";
        Status = TeamStatus.Draft;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public AgentTeamDefinition(
        TenantId tenantId,
        string teamName,
        string? description,
        string owner,
        string collaboratorsJson,
        string defaultModelPolicyJson,
        string budgetPolicyJson,
        string permissionScopeJson,
        TeamRiskLevel riskLevel,
        string tagsJson,
        long id)
        : base(tenantId)
    {
        Id = id;
        TeamName = teamName;
        Description = description ?? string.Empty;
        Owner = owner;
        CollaboratorsJson = string.IsNullOrWhiteSpace(collaboratorsJson) ? "[]" : collaboratorsJson;
        PublishStatus = TeamPublishStatus.Unpublished;
        DefaultModelPolicyJson = string.IsNullOrWhiteSpace(defaultModelPolicyJson) ? "{}" : defaultModelPolicyJson;
        BudgetPolicyJson = string.IsNullOrWhiteSpace(budgetPolicyJson) ? "{}" : budgetPolicyJson;
        PermissionScopeJson = string.IsNullOrWhiteSpace(permissionScopeJson) ? "{}" : permissionScopeJson;
        RiskLevel = riskLevel;
        TagsJson = string.IsNullOrWhiteSpace(tagsJson) ? "[]" : tagsJson;
        Status = TeamStatus.Draft;
        Version = 1;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string TeamName { get; private set; }
    public string? Description { get; private set; }
    public string Owner { get; private set; }
    public string CollaboratorsJson { get; private set; }
    public TeamStatus Status { get; private set; }
    public int Version { get; private set; }
    public long? PublishedVersionId { get; private set; }
    public TeamPublishStatus PublishStatus { get; private set; }
    public string DefaultModelPolicyJson { get; private set; }
    public string BudgetPolicyJson { get; private set; }
    public string PermissionScopeJson { get; private set; }
    public TeamRiskLevel RiskLevel { get; private set; }
    public string TagsJson { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Update(
        string teamName,
        string? description,
        string owner,
        string collaboratorsJson,
        string defaultModelPolicyJson,
        string budgetPolicyJson,
        string permissionScopeJson,
        TeamRiskLevel riskLevel,
        string tagsJson)
    {
        TeamName = teamName;
        Description = description ?? string.Empty;
        Owner = owner;
        CollaboratorsJson = string.IsNullOrWhiteSpace(collaboratorsJson) ? "[]" : collaboratorsJson;
        DefaultModelPolicyJson = string.IsNullOrWhiteSpace(defaultModelPolicyJson) ? "{}" : defaultModelPolicyJson;
        BudgetPolicyJson = string.IsNullOrWhiteSpace(budgetPolicyJson) ? "{}" : budgetPolicyJson;
        PermissionScopeJson = string.IsNullOrWhiteSpace(permissionScopeJson) ? "{}" : permissionScopeJson;
        RiskLevel = riskLevel;
        TagsJson = string.IsNullOrWhiteSpace(tagsJson) ? "[]" : tagsJson;
        Version++;
        if (Status == TeamStatus.Ready)
        {
            Status = TeamStatus.Draft;
        }

        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkReady()
    {
        Status = TeamStatus.Ready;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkPublished(long teamVersionId)
    {
        PublishedVersionId = teamVersionId;
        PublishStatus = TeamPublishStatus.Published;
        Status = TeamStatus.Published;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Disable()
    {
        Status = TeamStatus.Disabled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Enable()
    {
        Status = TeamStatus.Published;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        Status = TeamStatus.Archived;
        UpdatedAt = DateTime.UtcNow;
    }
}

public sealed class SubAgentDefinition : TenantEntity
{
    public SubAgentDefinition()
        : base(TenantId.Empty)
    {
        AgentName = string.Empty;
        Role = string.Empty;
        Goal = string.Empty;
        Boundaries = string.Empty;
        PromptTemplate = string.Empty;
        PromptVersion = 1;
        ModelConfigJson = "{}";
        ToolPermissionsJson = "[]";
        KnowledgeScopesJson = "[]";
        InputSchemaJson = "{}";
        OutputSchemaJson = "{}";
        MemoryPolicyJson = "{}";
        TimeoutPolicyJson = "{}";
        RetryPolicyJson = "{}";
        FallbackPolicyJson = "{}";
        VisibilityPolicyJson = "{}";
        Status = SubAgentStatus.Pending;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public SubAgentDefinition(
        TenantId tenantId,
        long teamId,
        string agentName,
        string role,
        string goal,
        string promptTemplate,
        string modelConfigJson,
        string inputSchemaJson,
        string outputSchemaJson,
        string timeoutPolicyJson,
        long id)
        : base(tenantId)
    {
        Id = id;
        TeamId = teamId;
        AgentName = agentName;
        Role = role;
        Goal = goal;
        Boundaries = string.Empty;
        PromptTemplate = promptTemplate;
        PromptVersion = 1;
        ModelConfigJson = string.IsNullOrWhiteSpace(modelConfigJson) ? "{}" : modelConfigJson;
        ToolPermissionsJson = "[]";
        KnowledgeScopesJson = "[]";
        InputSchemaJson = string.IsNullOrWhiteSpace(inputSchemaJson) ? "{}" : inputSchemaJson;
        OutputSchemaJson = string.IsNullOrWhiteSpace(outputSchemaJson) ? "{}" : outputSchemaJson;
        MemoryPolicyJson = "{}";
        TimeoutPolicyJson = string.IsNullOrWhiteSpace(timeoutPolicyJson) ? "{}" : timeoutPolicyJson;
        RetryPolicyJson = "{}";
        FallbackPolicyJson = "{}";
        VisibilityPolicyJson = "{}";
        Status = SubAgentStatus.Configured;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public void RebindTeam(long teamId)
    {
        TeamId = teamId;
        UpdatedAt = DateTime.UtcNow;
    }

    public long TeamId { get; private set; }
    public string AgentName { get; private set; }
    public string Role { get; private set; }
    public string Goal { get; private set; }
    public string? Boundaries { get; private set; }
    public string PromptTemplate { get; private set; }
    public int PromptVersion { get; private set; }
    public string ModelConfigJson { get; private set; }
    public string ToolPermissionsJson { get; private set; }
    public string KnowledgeScopesJson { get; private set; }
    public string InputSchemaJson { get; private set; }
    public string OutputSchemaJson { get; private set; }
    public string MemoryPolicyJson { get; private set; }
    public string TimeoutPolicyJson { get; private set; }
    public string RetryPolicyJson { get; private set; }
    public string FallbackPolicyJson { get; private set; }
    public string VisibilityPolicyJson { get; private set; }
    public SubAgentStatus Status { get; private set; }
    public long? TemplateSourceId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Update(
        string agentName,
        string role,
        string goal,
        string? boundaries,
        string promptTemplate,
        string modelConfigJson,
        string toolPermissionsJson,
        string knowledgeScopesJson,
        string inputSchemaJson,
        string outputSchemaJson,
        string memoryPolicyJson,
        string timeoutPolicyJson,
        string retryPolicyJson,
        string fallbackPolicyJson,
        string visibilityPolicyJson,
        SubAgentStatus status)
    {
        AgentName = agentName;
        Role = role;
        Goal = goal;
        Boundaries = boundaries ?? string.Empty;
        PromptTemplate = promptTemplate;
        PromptVersion++;
        ModelConfigJson = string.IsNullOrWhiteSpace(modelConfigJson) ? "{}" : modelConfigJson;
        ToolPermissionsJson = string.IsNullOrWhiteSpace(toolPermissionsJson) ? "[]" : toolPermissionsJson;
        KnowledgeScopesJson = string.IsNullOrWhiteSpace(knowledgeScopesJson) ? "[]" : knowledgeScopesJson;
        InputSchemaJson = string.IsNullOrWhiteSpace(inputSchemaJson) ? "{}" : inputSchemaJson;
        OutputSchemaJson = string.IsNullOrWhiteSpace(outputSchemaJson) ? "{}" : outputSchemaJson;
        MemoryPolicyJson = string.IsNullOrWhiteSpace(memoryPolicyJson) ? "{}" : memoryPolicyJson;
        TimeoutPolicyJson = string.IsNullOrWhiteSpace(timeoutPolicyJson) ? "{}" : timeoutPolicyJson;
        RetryPolicyJson = string.IsNullOrWhiteSpace(retryPolicyJson) ? "{}" : retryPolicyJson;
        FallbackPolicyJson = string.IsNullOrWhiteSpace(fallbackPolicyJson) ? "{}" : fallbackPolicyJson;
        VisibilityPolicyJson = string.IsNullOrWhiteSpace(visibilityPolicyJson) ? "{}" : visibilityPolicyJson;
        Status = status;
        UpdatedAt = DateTime.UtcNow;
    }
}

public sealed class OrchestrationNodeDefinition : TenantEntity
{
    public OrchestrationNodeDefinition()
        : base(TenantId.Empty)
    {
        NodeName = string.Empty;
        NodeType = NodeType.SubAgent;
        DependenciesJson = "[]";
        ExecutionMode = NodeExecutionMode.Sequential;
        ConditionExpression = string.Empty;
        InputBindingJson = "{}";
        OutputBindingJson = "{}";
        RetryRuleJson = "{}";
        TimeoutRuleJson = "{}";
        Priority = 0;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public OrchestrationNodeDefinition(
        TenantId tenantId,
        long teamId,
        string nodeName,
        NodeType nodeType,
        long? bindAgentId,
        string dependenciesJson,
        NodeExecutionMode executionMode,
        string? conditionExpression,
        string inputBindingJson,
        string outputBindingJson,
        string retryRuleJson,
        string timeoutRuleJson,
        bool humanApprovalRequired,
        long? fallbackNodeId,
        int priority,
        bool isCritical,
        bool skipAllowed,
        long id)
        : base(tenantId)
    {
        Id = id;
        Assign(
            teamId,
            nodeName,
            nodeType,
            bindAgentId,
            dependenciesJson,
            executionMode,
            conditionExpression,
            inputBindingJson,
            outputBindingJson,
            retryRuleJson,
            timeoutRuleJson,
            humanApprovalRequired,
            fallbackNodeId,
            priority,
            isCritical,
            skipAllowed);
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public long TeamId { get; private set; }
    public string NodeName { get; private set; } = string.Empty;
    public NodeType NodeType { get; private set; }
    public long? BindAgentId { get; private set; }
    public string DependenciesJson { get; private set; } = "[]";
    public NodeExecutionMode ExecutionMode { get; private set; }
    public string? ConditionExpression { get; private set; }
    public string InputBindingJson { get; private set; } = "{}";
    public string OutputBindingJson { get; private set; } = "{}";
    public string RetryRuleJson { get; private set; } = "{}";
    public string TimeoutRuleJson { get; private set; } = "{}";
    public bool HumanApprovalRequired { get; private set; }
    public long? FallbackNodeId { get; private set; }
    public int Priority { get; private set; }
    public bool IsCritical { get; private set; }
    public bool SkipAllowed { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Assign(
        long teamId,
        string nodeName,
        NodeType nodeType,
        long? bindAgentId,
        string dependenciesJson,
        NodeExecutionMode executionMode,
        string? conditionExpression,
        string inputBindingJson,
        string outputBindingJson,
        string retryRuleJson,
        string timeoutRuleJson,
        bool humanApprovalRequired,
        long? fallbackNodeId,
        int priority,
        bool isCritical,
        bool skipAllowed)
    {
        TeamId = teamId;
        NodeName = nodeName;
        NodeType = nodeType;
        BindAgentId = bindAgentId;
        DependenciesJson = string.IsNullOrWhiteSpace(dependenciesJson) ? "[]" : dependenciesJson;
        ExecutionMode = executionMode;
        ConditionExpression = conditionExpression ?? string.Empty;
        InputBindingJson = string.IsNullOrWhiteSpace(inputBindingJson) ? "{}" : inputBindingJson;
        OutputBindingJson = string.IsNullOrWhiteSpace(outputBindingJson) ? "{}" : outputBindingJson;
        RetryRuleJson = string.IsNullOrWhiteSpace(retryRuleJson) ? "{}" : retryRuleJson;
        TimeoutRuleJson = string.IsNullOrWhiteSpace(timeoutRuleJson) ? "{}" : timeoutRuleJson;
        HumanApprovalRequired = humanApprovalRequired;
        FallbackNodeId = fallbackNodeId;
        Priority = priority;
        IsCritical = isCritical;
        SkipAllowed = skipAllowed;
        UpdatedAt = DateTime.UtcNow;
    }
}

public sealed class TeamVersion : TenantEntity
{
    public TeamVersion()
        : base(TenantId.Empty)
    {
        VersionNo = string.Empty;
        DefinitionSnapshotJson = "{}";
        PublishStatus = TeamPublishStatus.Unpublished;
        PublishedBy = string.Empty;
        ApprovedBy = string.Empty;
        ApprovalRecordId = string.Empty;
    }

    public TeamVersion(
        TenantId tenantId,
        long teamId,
        string versionNo,
        int sourceDraftVersion,
        string definitionSnapshotJson,
        string publishedBy,
        string? approvedBy,
        string? approvalRecordId,
        long? rollbackFromVersionId,
        long id)
        : base(tenantId)
    {
        Id = id;
        Publish(
            teamId,
            versionNo,
            sourceDraftVersion,
            definitionSnapshotJson,
            publishedBy,
            approvedBy,
            approvalRecordId,
            rollbackFromVersionId);
    }

    public long TeamId { get; private set; }
    public string VersionNo { get; private set; } = string.Empty;
    public int SourceDraftVersion { get; private set; }
    public string DefinitionSnapshotJson { get; private set; } = "{}";
    public TeamPublishStatus PublishStatus { get; private set; }
    public string? PublishedBy { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public string? ApprovedBy { get; private set; }
    public string? ApprovalRecordId { get; private set; }
    public long? RollbackFromVersionId { get; private set; }

    public void Publish(
        long teamId,
        string versionNo,
        int sourceDraftVersion,
        string definitionSnapshotJson,
        string publishedBy,
        string? approvedBy,
        string? approvalRecordId,
        long? rollbackFromVersionId)
    {
        TeamId = teamId;
        VersionNo = versionNo;
        SourceDraftVersion = sourceDraftVersion;
        DefinitionSnapshotJson = string.IsNullOrWhiteSpace(definitionSnapshotJson) ? "{}" : definitionSnapshotJson;
        PublishStatus = TeamPublishStatus.Published;
        PublishedBy = publishedBy;
        PublishedAt = DateTime.UtcNow;
        ApprovedBy = approvedBy ?? string.Empty;
        ApprovalRecordId = approvalRecordId ?? string.Empty;
        RollbackFromVersionId = rollbackFromVersionId;
    }
}

public sealed class ExecutionRun : TenantEntity
{
    public ExecutionRun()
        : base(TenantId.Empty)
    {
        TriggerBy = string.Empty;
        TriggerType = TriggerType.Manual;
        InputPayloadJson = "{}";
        CurrentState = RunStatus.Pending;
        CurrentActiveNodesJson = "[]";
        OutputResultJson = "{}";
        OutputSummary = string.Empty;
        ErrorRecordsJson = "[]";
        CostStatsJson = "{}";
        TokenStatsJson = "{}";
        InterventionRecordsJson = "[]";
        ExecutionPlanJson = "{}";
        FinalDecision = string.Empty;
        StartedAt = DateTime.UtcNow;
    }

    public ExecutionRun(
        TenantId tenantId,
        long teamId,
        long teamVersionId,
        string triggerBy,
        TriggerType triggerType,
        string inputPayloadJson,
        long id)
        : base(tenantId)
    {
        Id = id;
        Start(teamId, teamVersionId, triggerBy, triggerType, inputPayloadJson);
    }

    public long TeamId { get; private set; }
    public long TeamVersionId { get; private set; }
    public string TriggerBy { get; private set; } = string.Empty;
    public TriggerType TriggerType { get; private set; }
    public string InputPayloadJson { get; private set; } = "{}";
    public RunStatus CurrentState { get; private set; }
    public string CurrentActiveNodesJson { get; private set; } = "[]";
    public string OutputResultJson { get; private set; } = "{}";
    public string? OutputSummary { get; private set; }
    public string ErrorRecordsJson { get; private set; } = "[]";
    public string CostStatsJson { get; private set; } = "{}";
    public string TokenStatsJson { get; private set; } = "{}";
    public string InterventionRecordsJson { get; private set; } = "[]";
    public string ExecutionPlanJson { get; private set; } = "{}";
    public string? FinalDecision { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public DateTime? ArchivedAt { get; private set; }

    public void Start(long teamId, long teamVersionId, string triggerBy, TriggerType triggerType, string inputPayloadJson)
    {
        TeamId = teamId;
        TeamVersionId = teamVersionId;
        TriggerBy = triggerBy;
        TriggerType = triggerType;
        InputPayloadJson = string.IsNullOrWhiteSpace(inputPayloadJson) ? "{}" : inputPayloadJson;
        CurrentState = RunStatus.Pending;
        StartedAt = DateTime.UtcNow;
    }

    public void TransitionTo(RunStatus status)
    {
        CurrentState = status;
        if (status is RunStatus.Completed or RunStatus.Failed or RunStatus.Cancelled or RunStatus.TimedOut)
        {
            EndedAt = DateTime.UtcNow;
        }
    }

    public void Complete(string outputResultJson, string? outputSummary, string? finalDecision)
    {
        OutputResultJson = string.IsNullOrWhiteSpace(outputResultJson) ? "{}" : outputResultJson;
        OutputSummary = outputSummary ?? string.Empty;
        FinalDecision = finalDecision ?? string.Empty;
        TransitionTo(RunStatus.Completed);
    }

    public void Fail(string errorRecordsJson)
    {
        ErrorRecordsJson = string.IsNullOrWhiteSpace(errorRecordsJson) ? "[]" : errorRecordsJson;
        TransitionTo(RunStatus.Failed);
    }
}

public sealed class NodeRun : TenantEntity
{
    public NodeRun()
        : base(TenantId.Empty)
    {
        State = NodeRunStatus.Idle;
        InputSnapshotJson = "{}";
        OutputSnapshotJson = "{}";
        ToolCallRecordsJson = "[]";
        RetrievalRecordsJson = "[]";
        ErrorCode = string.Empty;
        ErrorMessage = string.Empty;
        InterventionRecordIdsJson = "[]";
    }

    public NodeRun(
        TenantId tenantId,
        long runId,
        long nodeId,
        long? agentId,
        string inputSnapshotJson,
        bool humanInterventionAllowed,
        long id)
        : base(tenantId)
    {
        Id = id;
        Start(runId, nodeId, agentId, inputSnapshotJson, humanInterventionAllowed);
    }

    public long RunId { get; private set; }
    public long NodeId { get; private set; }
    public long? AgentId { get; private set; }
    public NodeRunStatus State { get; private set; }
    public string InputSnapshotJson { get; private set; } = "{}";
    public string OutputSnapshotJson { get; private set; } = "{}";
    public string ToolCallRecordsJson { get; private set; } = "[]";
    public string RetrievalRecordsJson { get; private set; } = "[]";
    public int RetryCount { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public bool HumanInterventionAllowed { get; private set; }
    public string InterventionRecordIdsJson { get; private set; } = "[]";

    public void Start(long runId, long nodeId, long? agentId, string inputSnapshotJson, bool humanInterventionAllowed)
    {
        RunId = runId;
        NodeId = nodeId;
        AgentId = agentId;
        State = NodeRunStatus.Running;
        InputSnapshotJson = string.IsNullOrWhiteSpace(inputSnapshotJson) ? "{}" : inputSnapshotJson;
        HumanInterventionAllowed = humanInterventionAllowed;
        StartedAt = DateTime.UtcNow;
    }

    public void Succeed(string outputSnapshotJson)
    {
        State = NodeRunStatus.Succeeded;
        OutputSnapshotJson = string.IsNullOrWhiteSpace(outputSnapshotJson) ? "{}" : outputSnapshotJson;
        EndedAt = DateTime.UtcNow;
    }

    public void Fail(string? errorCode, string? errorMessage)
    {
        State = NodeRunStatus.Failed;
        ErrorCode = errorCode ?? string.Empty;
        ErrorMessage = errorMessage ?? string.Empty;
        EndedAt = DateTime.UtcNow;
    }

    public void Retry()
    {
        RetryCount++;
        State = NodeRunStatus.Retrying;
    }

    public void WaitApproval()
    {
        State = NodeRunStatus.WaitingApproval;
    }

    public void Skip()
    {
        State = NodeRunStatus.Skipped;
        EndedAt = DateTime.UtcNow;
    }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TeamStatus
{
    Draft = 0,
    Ready = 1,
    Published = 2,
    Disabled = 3,
    Archived = 4
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TeamPublishStatus
{
    Unpublished = 0,
    PendingApproval = 1,
    Published = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TeamRiskLevel
{
    Low = 0,
    Medium = 1,
    High = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SubAgentStatus
{
    Pending = 0,
    Configured = 1,
    Conflict = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NodeType
{
    SubAgent = 0,
    Tool = 1,
    Knowledge = 2,
    Condition = 3,
    HumanApproval = 4,
    Aggregation = 5
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NodeExecutionMode
{
    Sequential = 0,
    Parallel = 1,
    Conditional = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TriggerType
{
    Manual = 0,
    Api = 1,
    Schedule = 2,
    Event = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RunStatus
{
    Pending = 0,
    Planning = 1,
    Dispatching = 2,
    Running = 3,
    WaitingTool = 4,
    WaitingHuman = 5,
    Retrying = 6,
    PartiallyFailed = 7,
    Failed = 8,
    Completed = 9,
    Cancelled = 10,
    TimedOut = 11
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NodeRunStatus
{
    Idle = 0,
    Ready = 1,
    WaitingDependency = 2,
    Assigned = 3,
    Running = 4,
    WaitingInput = 5,
    WaitingTool = 6,
    WaitingApproval = 7,
    Retrying = 8,
    Succeeded = 9,
    Failed = 10,
    Skipped = 11,
    Cancelled = 12
}

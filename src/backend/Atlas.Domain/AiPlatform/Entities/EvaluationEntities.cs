using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class EvaluationDataset : TenantEntity
{
    public EvaluationDataset()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        Description = string.Empty;
        Scene = string.Empty;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public EvaluationDataset(
        TenantId tenantId,
        string name,
        string? description,
        string? scene,
        long createdByUserId,
        long id)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        Description = description ?? string.Empty;
        Scene = scene ?? string.Empty;
        CreatedByUserId = createdByUserId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string Name { get; private set; }
    public string Description { get; private set; }
    public string Scene { get; private set; }
    public long CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
}

public sealed class EvaluationCase : TenantEntity
{
    public EvaluationCase()
        : base(TenantId.Empty)
    {
        Input = string.Empty;
        ExpectedOutput = string.Empty;
        ReferenceOutput = string.Empty;
        TagsJson = "[]";
        GroundTruthChunkIdsJson = "[]";
        GroundTruthCitationsJson = "[]";
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public EvaluationCase(
        TenantId tenantId,
        long datasetId,
        string input,
        string? expectedOutput,
        string? referenceOutput,
        string? tagsJson,
        string? groundTruthChunkIdsJson,
        string? groundTruthCitationsJson,
        long id)
        : base(tenantId)
    {
        Id = id;
        DatasetId = datasetId;
        Input = input;
        ExpectedOutput = expectedOutput ?? string.Empty;
        ReferenceOutput = referenceOutput ?? string.Empty;
        TagsJson = string.IsNullOrWhiteSpace(tagsJson) ? "[]" : tagsJson;
        GroundTruthChunkIdsJson = string.IsNullOrWhiteSpace(groundTruthChunkIdsJson) ? "[]" : groundTruthChunkIdsJson;
        GroundTruthCitationsJson = string.IsNullOrWhiteSpace(groundTruthCitationsJson) ? "[]" : groundTruthCitationsJson;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public long DatasetId { get; private set; }
    public string Input { get; private set; }
    public string ExpectedOutput { get; private set; }
    public string ReferenceOutput { get; private set; }
    [SugarColumn(ColumnDataType = "TEXT")]
    public string TagsJson { get; private set; }
    [SugarColumn(ColumnDataType = "TEXT")]
    public string GroundTruthChunkIdsJson { get; private set; }
    [SugarColumn(ColumnDataType = "TEXT")]
    public string GroundTruthCitationsJson { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
}

public sealed class EvaluationTask : TenantEntity
{
    public EvaluationTask()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        ErrorMessage = string.Empty;
        AggregateMetricsJson = "{}";
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        StartedAt = DateTime.UnixEpoch;
        CompletedAt = DateTime.UnixEpoch;
    }

    public EvaluationTask(
        TenantId tenantId,
        string name,
        long datasetId,
        long agentId,
        bool enableRag,
        long createdByUserId,
        long id)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        DatasetId = datasetId;
        AgentId = agentId;
        EnableRag = enableRag;
        CreatedByUserId = createdByUserId;
        Status = EvaluationTaskStatus.Pending;
        TotalCases = 0;
        CompletedCases = 0;
        Score = 0;
        ErrorMessage = string.Empty;
        AggregateMetricsJson = "{}";
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        StartedAt = DateTime.UnixEpoch;
        CompletedAt = DateTime.UnixEpoch;
    }

    public string Name { get; private set; }
    public long DatasetId { get; private set; }
    public long AgentId { get; private set; }
    public bool EnableRag { get; private set; }
    public long CreatedByUserId { get; private set; }
    public EvaluationTaskStatus Status { get; private set; }
    public int TotalCases { get; private set; }
    public int CompletedCases { get; private set; }
    public decimal Score { get; private set; }
    public string ErrorMessage { get; private set; }
    [SugarColumn(ColumnDataType = "TEXT")]
    public string AggregateMetricsJson { get; private set; }

    /// <summary>
    /// Coze PRD（M5.1）：评测任务所属工作空间。Nullable 保证历史数据兼容：
    /// 早期评测任务（EvaluationService 创建的 agent 评测）未知工作空间，保留 null；
    /// 新由 Coze API 创建的任务必须赋值以便按 workspace 隔离。
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = true)]
    public string? WorkspaceId { get; private set; }

    public void AttachWorkspace(string workspaceId)
    {
        if (!string.IsNullOrWhiteSpace(workspaceId))
        {
            WorkspaceId = workspaceId.Trim();
            UpdatedAt = DateTime.UtcNow;
        }
    }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime CompletedAt { get; private set; }

    public void MarkRunning(int totalCases)
    {
        Status = EvaluationTaskStatus.Running;
        TotalCases = totalCases;
        CompletedCases = 0;
        StartedAt = DateTime.UtcNow;
        UpdatedAt = StartedAt;
        ErrorMessage = string.Empty;
    }

    public void MarkCompleted(int completedCases, decimal score, string? aggregateMetricsJson = null)
    {
        Status = EvaluationTaskStatus.Completed;
        CompletedCases = completedCases;
        Score = score;
        if (!string.IsNullOrWhiteSpace(aggregateMetricsJson))
        {
            AggregateMetricsJson = aggregateMetricsJson;
        }

        CompletedAt = DateTime.UtcNow;
        UpdatedAt = CompletedAt;
        ErrorMessage = string.Empty;
    }

    public void MarkFailed(string? errorMessage, int completedCases)
    {
        Status = EvaluationTaskStatus.Failed;
        CompletedCases = completedCases;
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? "评测任务失败" : errorMessage;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = CompletedAt;
    }
}

public sealed class EvaluationResult : TenantEntity
{
    public EvaluationResult()
        : base(TenantId.Empty)
    {
        ActualOutput = string.Empty;
        JudgeReason = string.Empty;
        RagMetricsJson = "{}";
        CreatedAt = DateTime.UtcNow;
    }

    public EvaluationResult(
        TenantId tenantId,
        long taskId,
        long caseId,
        string? actualOutput,
        decimal score,
        string? judgeReason,
        decimal faithfulnessScore,
        decimal contextPrecisionScore,
        decimal contextRecallScore,
        decimal answerRelevanceScore,
        decimal citationAccuracyScore,
        decimal hallucinationScore,
        string? ragMetricsJson,
        EvaluationCaseStatus status,
        long id)
        : base(tenantId)
    {
        Id = id;
        TaskId = taskId;
        CaseId = caseId;
        ActualOutput = actualOutput ?? string.Empty;
        Score = score;
        JudgeReason = judgeReason ?? string.Empty;
        FaithfulnessScore = faithfulnessScore;
        ContextPrecisionScore = contextPrecisionScore;
        ContextRecallScore = contextRecallScore;
        AnswerRelevanceScore = answerRelevanceScore;
        CitationAccuracyScore = citationAccuracyScore;
        HallucinationScore = hallucinationScore;
        RagMetricsJson = string.IsNullOrWhiteSpace(ragMetricsJson) ? "{}" : ragMetricsJson;
        Status = status;
        CreatedAt = DateTime.UtcNow;
    }

    public long TaskId { get; private set; }
    public long CaseId { get; private set; }
    public string ActualOutput { get; private set; }
    public decimal Score { get; private set; }
    public string JudgeReason { get; private set; }
    public decimal FaithfulnessScore { get; private set; }
    public decimal ContextPrecisionScore { get; private set; }
    public decimal ContextRecallScore { get; private set; }
    public decimal AnswerRelevanceScore { get; private set; }
    public decimal CitationAccuracyScore { get; private set; }
    public decimal HallucinationScore { get; private set; }
    [SugarColumn(ColumnDataType = "TEXT")]
    public string RagMetricsJson { get; private set; }
    public EvaluationCaseStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
}

public enum EvaluationTaskStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3
}

public enum EvaluationCaseStatus
{
    Pending = 0,
    Passed = 1,
    Failed = 2,
    Error = 3
}

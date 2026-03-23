using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

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
        long id)
        : base(tenantId)
    {
        Id = id;
        DatasetId = datasetId;
        Input = input;
        ExpectedOutput = expectedOutput ?? string.Empty;
        ReferenceOutput = referenceOutput ?? string.Empty;
        TagsJson = string.IsNullOrWhiteSpace(tagsJson) ? "[]" : tagsJson;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public long DatasetId { get; private set; }
    public string Input { get; private set; }
    public string ExpectedOutput { get; private set; }
    public string ReferenceOutput { get; private set; }
    public string TagsJson { get; private set; }
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
        long createdByUserId,
        long id)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        DatasetId = datasetId;
        AgentId = agentId;
        CreatedByUserId = createdByUserId;
        Status = EvaluationTaskStatus.Pending;
        TotalCases = 0;
        CompletedCases = 0;
        Score = 0;
        ErrorMessage = string.Empty;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        StartedAt = DateTime.UnixEpoch;
        CompletedAt = DateTime.UnixEpoch;
    }

    public string Name { get; private set; }
    public long DatasetId { get; private set; }
    public long AgentId { get; private set; }
    public long CreatedByUserId { get; private set; }
    public EvaluationTaskStatus Status { get; private set; }
    public int TotalCases { get; private set; }
    public int CompletedCases { get; private set; }
    public decimal Score { get; private set; }
    public string ErrorMessage { get; private set; }
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

    public void MarkCompleted(int completedCases, decimal score)
    {
        Status = EvaluationTaskStatus.Completed;
        CompletedCases = completedCases;
        Score = score;
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
        CreatedAt = DateTime.UtcNow;
    }

    public EvaluationResult(
        TenantId tenantId,
        long taskId,
        long caseId,
        string? actualOutput,
        decimal score,
        string? judgeReason,
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
        Status = status;
        CreatedAt = DateTime.UtcNow;
    }

    public long TaskId { get; private set; }
    public long CaseId { get; private set; }
    public string ActualOutput { get; private set; }
    public decimal Score { get; private set; }
    public string JudgeReason { get; private set; }
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

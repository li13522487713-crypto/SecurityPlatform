using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.BatchProcess.Enums;

namespace Atlas.Domain.BatchProcess.Entities;

/// <summary>
/// 批处理任务的一次执行实例。
/// </summary>
public sealed class BatchJobExecution : TenantEntity
{
    public BatchJobExecution()
        : base(TenantId.Empty)
    {
        TriggeredBy = string.Empty;
    }

    public BatchJobExecution(TenantId tenantId, long id, long jobDefinitionId, string triggeredBy)
        : base(tenantId)
    {
        Id = id;
        JobDefinitionId = jobDefinitionId;
        TriggeredBy = triggeredBy;
        Status = JobExecutionStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public long JobDefinitionId { get; private set; }
    public JobExecutionStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public int TotalShards { get; private set; }
    public int CompletedShards { get; private set; }
    public int FailedShards { get; private set; }
    public long TotalRecords { get; private set; }
    public long ProcessedRecords { get; private set; }
    public long FailedRecords { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string TriggeredBy { get; private set; }

    public void MarkRunning(int totalShards, long totalRecords)
    {
        Status = JobExecutionStatus.Running;
        StartedAt = DateTime.UtcNow;
        TotalShards = totalShards;
        TotalRecords = totalRecords;
    }

    public void UpdateProgress(int completedShards, int failedShards, long processedRecords, long failedRecords)
    {
        CompletedShards = completedShards;
        FailedShards = failedShards;
        ProcessedRecords = processedRecords;
        FailedRecords = failedRecords;
    }

    public void MarkCompleted()
    {
        Status = JobExecutionStatus.Completed;
        CompletedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = JobExecutionStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        ErrorMessage = errorMessage;
    }

    public void MarkCancelled()
    {
        Status = JobExecutionStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
    }
}

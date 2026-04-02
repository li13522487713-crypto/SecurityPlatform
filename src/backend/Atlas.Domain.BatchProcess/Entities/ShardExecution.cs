using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.BatchProcess.Enums;

namespace Atlas.Domain.BatchProcess.Entities;

/// <summary>
/// 分片执行：一个 JobExecution 被拆分为多个 Shard 并行处理。
/// </summary>
public sealed class ShardExecution : TenantEntity
{
    public ShardExecution()
        : base(TenantId.Empty)
    {
        ShardKey = string.Empty;
    }

    public ShardExecution(TenantId tenantId, long id, long jobExecutionId, int shardIndex, string shardKey)
        : base(tenantId)
    {
        Id = id;
        JobExecutionId = jobExecutionId;
        ShardIndex = shardIndex;
        ShardKey = shardKey;
        Status = ShardExecutionStatus.Pending;
    }

    public long JobExecutionId { get; private set; }
    public int ShardIndex { get; private set; }
    public string ShardKey { get; private set; }
    public ShardExecutionStatus Status { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public long ProcessedRecords { get; private set; }
    public long FailedRecords { get; private set; }
    public long? LastCheckpointId { get; private set; }
    public int RetryCount { get; private set; }
    public string? ErrorMessage { get; private set; }

    public void MarkRunning()
    {
        Status = ShardExecutionStatus.Running;
        StartedAt = DateTime.UtcNow;
    }

    public void UpdateProgress(long processedRecords, long failedRecords)
    {
        ProcessedRecords = processedRecords;
        FailedRecords = failedRecords;
    }

    public void SetCheckpoint(long checkpointId)
    {
        LastCheckpointId = checkpointId;
    }

    public void MarkCompleted()
    {
        Status = ShardExecutionStatus.Completed;
        CompletedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = ShardExecutionStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        ErrorMessage = errorMessage;
    }

    public void MarkRetrying()
    {
        Status = ShardExecutionStatus.Retrying;
        RetryCount++;
        ErrorMessage = null;
        StartedAt = null;
        CompletedAt = null;
    }
}

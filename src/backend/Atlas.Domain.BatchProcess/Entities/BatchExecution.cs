using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.BatchProcess.Enums;

namespace Atlas.Domain.BatchProcess.Entities;

/// <summary>
/// 单个批次的执行记录：Shard 内按 BatchSize 拆分后的最小处理单元。
/// </summary>
public sealed class BatchExecution : TenantEntity
{
    public BatchExecution()
        : base(TenantId.Empty)
    {
    }

    public BatchExecution(TenantId tenantId, long id, long shardExecutionId, int batchIndex, int itemCount)
        : base(tenantId)
    {
        Id = id;
        ShardExecutionId = shardExecutionId;
        BatchIndex = batchIndex;
        ItemCount = itemCount;
        Status = BatchExecutionStatus.Pending;
    }

    public long ShardExecutionId { get; private set; }
    public int BatchIndex { get; private set; }
    public BatchExecutionStatus Status { get; private set; }
    public int ItemCount { get; private set; }
    public int ProcessedCount { get; private set; }
    public int FailedCount { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? ErrorMessage { get; private set; }

    public void MarkRunning()
    {
        Status = BatchExecutionStatus.Running;
        StartedAt = DateTime.UtcNow;
    }

    public void UpdateProgress(int processedCount, int failedCount)
    {
        ProcessedCount = processedCount;
        FailedCount = failedCount;
    }

    public void MarkCompleted()
    {
        Status = BatchExecutionStatus.Completed;
        CompletedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = BatchExecutionStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        ErrorMessage = errorMessage;
    }
}

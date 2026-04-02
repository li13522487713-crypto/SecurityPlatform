using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.BatchProcess.Entities;

/// <summary>
/// 检查点：记录分片处理进度，支持断点恢复。
/// </summary>
public sealed class BatchCheckpoint : TenantEntity
{
    public BatchCheckpoint()
        : base(TenantId.Empty)
    {
        CheckpointKey = string.Empty;
        ProcessedUpTo = string.Empty;
    }

    public BatchCheckpoint(
        TenantId tenantId,
        long id,
        long shardExecutionId,
        string checkpointKey,
        string processedUpTo,
        long processedCount)
        : base(tenantId)
    {
        Id = id;
        ShardExecutionId = shardExecutionId;
        CheckpointKey = checkpointKey;
        ProcessedUpTo = processedUpTo;
        ProcessedCount = processedCount;
        CreatedAt = DateTime.UtcNow;
    }

    public long ShardExecutionId { get; private set; }
    public string CheckpointKey { get; private set; }
    public string ProcessedUpTo { get; private set; }
    public long ProcessedCount { get; private set; }
    public DateTime CreatedAt { get; private set; }
}

using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.BatchProcess.Enums;

namespace Atlas.Domain.BatchProcess.Entities;

/// <summary>
/// 死信记录：处理失败且已耗尽重试的数据条目。
/// </summary>
public sealed class BatchDeadLetter : TenantEntity
{
    public BatchDeadLetter()
        : base(TenantId.Empty)
    {
        RecordKey = string.Empty;
        RecordPayload = string.Empty;
        ErrorType = string.Empty;
        ErrorMessage = string.Empty;
    }

    public BatchDeadLetter(
        TenantId tenantId,
        long id,
        long jobExecutionId,
        long shardExecutionId,
        long? batchExecutionId,
        string recordKey,
        string recordPayload,
        string errorType,
        string errorMessage,
        string? errorStackTrace,
        int maxRetries)
        : base(tenantId)
    {
        Id = id;
        JobExecutionId = jobExecutionId;
        ShardExecutionId = shardExecutionId;
        BatchExecutionId = batchExecutionId;
        RecordKey = recordKey;
        RecordPayload = recordPayload;
        ErrorType = errorType;
        ErrorMessage = errorMessage;
        ErrorStackTrace = errorStackTrace;
        MaxRetries = maxRetries;
        Status = DeadLetterStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public long JobExecutionId { get; private set; }
    public long ShardExecutionId { get; private set; }
    public long? BatchExecutionId { get; private set; }
    public string RecordKey { get; private set; }
    public string RecordPayload { get; private set; }
    public string ErrorType { get; private set; }
    public string ErrorMessage { get; private set; }
    public string? ErrorStackTrace { get; private set; }
    public int RetryCount { get; private set; }
    public int MaxRetries { get; private set; }
    public DeadLetterStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastRetryAt { get; private set; }

    public void MarkRetrying()
    {
        Status = DeadLetterStatus.Retrying;
        RetryCount++;
        LastRetryAt = DateTime.UtcNow;
    }

    public void MarkResolved()
    {
        Status = DeadLetterStatus.Resolved;
    }

    public void MarkAbandoned()
    {
        Status = DeadLetterStatus.Abandoned;
    }
}

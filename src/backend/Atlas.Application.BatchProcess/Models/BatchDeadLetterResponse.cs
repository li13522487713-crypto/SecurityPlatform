using Atlas.Domain.BatchProcess.Enums;

namespace Atlas.Application.BatchProcess.Models;

public sealed class BatchDeadLetterResponse
{
    public string Id { get; set; } = string.Empty;
    public string JobExecutionId { get; set; } = string.Empty;
    public string ShardExecutionId { get; set; } = string.Empty;
    public string? BatchExecutionId { get; set; }
    public string RecordKey { get; set; } = string.Empty;
    public string RecordPayload { get; set; } = string.Empty;
    public string ErrorType { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string? ErrorStackTrace { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; }
    public DeadLetterStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastRetryAt { get; set; }
}

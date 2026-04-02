using Atlas.Domain.BatchProcess.Enums;

namespace Atlas.Application.BatchProcess.Models;

public sealed class ShardExecutionResponse
{
    public string Id { get; set; } = string.Empty;
    public string JobExecutionId { get; set; } = string.Empty;
    public int ShardIndex { get; set; }
    public string ShardKey { get; set; } = string.Empty;
    public ShardExecutionStatus Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long ProcessedRecords { get; set; }
    public long FailedRecords { get; set; }
    public string? LastCheckpointId { get; set; }
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }
}

namespace Atlas.Application.BatchProcess.Models;

public sealed class KeysetScanRequest
{
    public required string TableName { get; init; }
    public required string KeyColumn { get; init; }
    public string? AfterKey { get; init; }
    public int PageSize { get; init; } = 1000;
    public string? FilterExpression { get; init; }
}

public sealed class KeysetScanResult
{
    public required IReadOnlyList<string> Keys { get; init; }
    public string? LastKey { get; init; }
    public bool HasMore { get; init; }
}

public sealed class ShardRange
{
    public required int ShardIndex { get; init; }
    public required string ShardKey { get; init; }
    public string? RangeStart { get; init; }
    public string? RangeEnd { get; init; }
    public long EstimatedCount { get; init; }
}

public sealed class ShardingRequest
{
    public required string TableName { get; init; }
    public required string KeyColumn { get; init; }
    public int DesiredShardCount { get; init; } = 4;
    public string? FilterExpression { get; init; }
}

public sealed class TimeWindowShardingRequest
{
    public required DateTime StartTime { get; init; }
    public required DateTime EndTime { get; init; }
    public required string TimeColumn { get; init; }
    public TimeSpan WindowSize { get; init; } = TimeSpan.FromHours(1);
}

public sealed class BatchSlice
{
    public required int BatchIndex { get; init; }
    public required long Offset { get; init; }
    public required int Count { get; init; }
}

public sealed class CheckpointInfo
{
    public required long CheckpointId { get; init; }
    public required string CheckpointKey { get; init; }
    public required string ProcessedUpTo { get; init; }
    public required long ProcessedCount { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public sealed class RecoveryResult
{
    public required bool Success { get; init; }
    public required long ShardExecutionId { get; init; }
    public CheckpointInfo? RestoredCheckpoint { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class RetryResult
{
    public required int TotalFailedShards { get; init; }
    public required int RetriedShards { get; init; }
    public required IReadOnlyList<long> RetriedShardIds { get; init; }
}

public sealed class BackpressureMetrics
{
    public int ActiveWorkers { get; init; }
    public int MaxConcurrency { get; init; }
    public double CpuUsagePercent { get; init; }
    public double MemoryUsagePercent { get; init; }
    public long PendingItems { get; init; }
    public double AverageLatencyMs { get; init; }
    public double ErrorRate { get; init; }
}

public sealed class BackpressureDecision
{
    public required bool ShouldThrottle { get; init; }
    public int RecommendedConcurrency { get; init; }
    public int RecommendedBatchSize { get; init; }
    public string? Reason { get; init; }
}

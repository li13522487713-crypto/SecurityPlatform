using Atlas.Domain.BatchProcess.Enums;

namespace Atlas.Application.BatchProcess.Models;

public sealed class BatchJobUpdateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DataSourceType { get; set; } = string.Empty;
    public string DataSourceConfig { get; set; } = string.Empty;
    public ShardStrategy ShardStrategyType { get; set; }
    public string ShardConfig { get; set; } = string.Empty;
    public int BatchSize { get; set; } = 100;
    public int MaxConcurrency { get; set; } = 4;
    public string RetryPolicy { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 3600;
    public string? CronExpression { get; set; }
}

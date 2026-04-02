using Atlas.Domain.BatchProcess.Enums;

namespace Atlas.Application.BatchProcess.Models;

public sealed class BatchJobDefinitionResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DataSourceType { get; set; } = string.Empty;
    public string DataSourceConfig { get; set; } = string.Empty;
    public ShardStrategy ShardStrategyType { get; set; }
    public string ShardConfig { get; set; } = string.Empty;
    public int BatchSize { get; set; }
    public int MaxConcurrency { get; set; }
    public string RetryPolicy { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; }
    public string? CronExpression { get; set; }
    public BatchJobStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.BatchProcess.Enums;

namespace Atlas.Domain.BatchProcess.Entities;

/// <summary>
/// 批处理任务定义：描述"做什么"和"怎么做"。
/// </summary>
public sealed class BatchJobDefinition : TenantEntity
{
    public BatchJobDefinition()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        DataSourceType = string.Empty;
        DataSourceConfig = string.Empty;
        ShardConfig = string.Empty;
        RetryPolicy = string.Empty;
        CreatedBy = string.Empty;
    }

    public BatchJobDefinition(
        TenantId tenantId,
        long id,
        string name,
        string? description,
        string dataSourceType,
        string dataSourceConfig,
        ShardStrategy shardStrategy,
        string shardConfig,
        int batchSize,
        int maxConcurrency,
        string retryPolicy,
        int timeoutSeconds,
        string? cronExpression,
        string createdBy)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        Description = description;
        DataSourceType = dataSourceType;
        DataSourceConfig = dataSourceConfig;
        ShardStrategyType = shardStrategy;
        ShardConfig = shardConfig;
        BatchSize = batchSize;
        MaxConcurrency = maxConcurrency;
        RetryPolicy = retryPolicy;
        TimeoutSeconds = timeoutSeconds;
        CronExpression = cronExpression;
        CreatedBy = createdBy;
        Status = BatchJobStatus.Draft;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string DataSourceType { get; private set; }
    public string DataSourceConfig { get; private set; }
    public ShardStrategy ShardStrategyType { get; private set; }
    public string ShardConfig { get; private set; }
    public int BatchSize { get; private set; }
    public int MaxConcurrency { get; private set; }
    public string RetryPolicy { get; private set; }
    public int TimeoutSeconds { get; private set; }
    public string? CronExpression { get; private set; }
    public BatchJobStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public string CreatedBy { get; private set; }

    public void Activate()
    {
        Status = BatchJobStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Pause()
    {
        Status = BatchJobStatus.Paused;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        Status = BatchJobStatus.Archived;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateDefinition(
        string name,
        string? description,
        string dataSourceType,
        string dataSourceConfig,
        ShardStrategy shardStrategy,
        string shardConfig,
        int batchSize,
        int maxConcurrency,
        string retryPolicy,
        int timeoutSeconds,
        string? cronExpression)
    {
        Name = name;
        Description = description;
        DataSourceType = dataSourceType;
        DataSourceConfig = dataSourceConfig;
        ShardStrategyType = shardStrategy;
        ShardConfig = shardConfig;
        BatchSize = batchSize;
        MaxConcurrency = maxConcurrency;
        RetryPolicy = retryPolicy;
        TimeoutSeconds = timeoutSeconds;
        CronExpression = cronExpression;
        UpdatedAt = DateTime.UtcNow;
    }
}

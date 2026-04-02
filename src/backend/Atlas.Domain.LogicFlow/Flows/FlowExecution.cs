using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LogicFlow.Flows;

[SugarTable("lf_flow_execution")]
public sealed class FlowExecution : TenantEntity
{
    public FlowExecution() : base(default) { }

    public FlowExecution(
        TenantId tenantId,
        long flowDefinitionId,
        string version,
        FlowTriggerType triggerType,
        string createdBy,
        string? inputDataJson,
        string? correlationId,
        int maxRetries,
        long? snapshotId,
        long? parentExecutionId)
        : base(tenantId)
    {
        FlowDefinitionId = flowDefinitionId;
        Version = version;
        TriggerType = triggerType;
        CreatedBy = createdBy;
        InputDataJson = string.IsNullOrWhiteSpace(inputDataJson) ? "{}" : inputDataJson!;
        CorrelationId = correlationId;
        MaxRetries = maxRetries;
        SnapshotId = snapshotId;
        ParentExecutionId = parentExecutionId;
        Status = ExecutionStatus.Pending;
        OutputDataJson = "{}";
        RetryCount = 0;
        CreatedAt = DateTime.UtcNow;
    }

    [SugarColumn(IsPrimaryKey = true)]
    public new long Id { get => base.Id; set => SetId(value); }

    public long FlowDefinitionId { get; set; }

    [SugarColumn(Length = 50)]
    public string Version { get; set; } = "1.0.0";

    public ExecutionStatus Status { get; set; }

    public FlowTriggerType TriggerType { get; set; }

    [SugarColumn(ColumnDataType = "text")]
    public string InputDataJson { get; set; } = "{}";

    [SugarColumn(ColumnDataType = "text")]
    public string OutputDataJson { get; set; } = "{}";

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ErrorMessage { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? DurationMs { get; set; }

    [SugarColumn(Length = 100, IsNullable = true)]
    public string? CurrentNodeKey { get; set; }

    public int RetryCount { get; set; }

    public int MaxRetries { get; set; } = 3;

    public long? SnapshotId { get; set; }

    [SugarColumn(Length = 100, IsNullable = true)]
    public string? CorrelationId { get; set; }

    [SugarColumn(Length = 100)]
    public string CreatedBy { get; set; } = string.Empty;

    public long? ParentExecutionId { get; set; }

    public void Start()
    {
        Status = ExecutionStatus.Running;
        StartedAt = DateTime.UtcNow;
    }

    public void Complete(string? outputJson)
    {
        OutputDataJson = string.IsNullOrWhiteSpace(outputJson) ? "{}" : outputJson!;
        Status = ExecutionStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        if (StartedAt.HasValue)
            DurationMs = (long)(CompletedAt.Value - StartedAt.Value).TotalMilliseconds;
    }

    public void Fail(string errorMessage)
    {
        ErrorMessage = errorMessage;
        Status = ExecutionStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        if (StartedAt.HasValue)
            DurationMs = (long)(CompletedAt.Value - StartedAt.Value).TotalMilliseconds;
    }

    public void Cancel()
    {
        Status = ExecutionStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
        if (StartedAt.HasValue)
            DurationMs = (long)(CompletedAt.Value - StartedAt.Value).TotalMilliseconds;
    }

    public void Timeout()
    {
        Status = ExecutionStatus.TimedOut;
        CompletedAt = DateTime.UtcNow;
        if (StartedAt.HasValue)
            DurationMs = (long)(CompletedAt.Value - StartedAt.Value).TotalMilliseconds;
    }

    public void Pause()
    {
        Status = ExecutionStatus.Paused;
    }

    public void Resume()
    {
        if (Status == ExecutionStatus.Paused)
            Status = ExecutionStatus.Running;
    }
}

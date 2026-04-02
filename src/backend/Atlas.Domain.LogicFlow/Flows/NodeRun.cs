using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LogicFlow.Flows;

[SugarTable("lf_node_run")]
public sealed class NodeRun : TenantEntity
{
    public NodeRun() : base(default) { }

    public NodeRun(
        TenantId tenantId,
        long flowExecutionId,
        string nodeKey,
        string nodeTypeKey,
        string? inputDataJson,
        int maxRetries)
        : base(tenantId)
    {
        FlowExecutionId = flowExecutionId;
        NodeKey = nodeKey;
        NodeTypeKey = nodeTypeKey;
        InputDataJson = string.IsNullOrWhiteSpace(inputDataJson) ? "{}" : inputDataJson!;
        OutputDataJson = "{}";
        MaxRetries = maxRetries;
        RetryCount = 0;
        Status = NodeRunStatus.Pending;
        IsCompensated = false;
    }

    [SugarColumn(IsPrimaryKey = true)]
    public new long Id { get => base.Id; set => SetId(value); }

    public long FlowExecutionId { get; set; }

    [SugarColumn(Length = 100)]
    public string NodeKey { get; set; } = string.Empty;

    [SugarColumn(Length = 100)]
    public string NodeTypeKey { get; set; } = string.Empty;

    public NodeRunStatus Status { get; set; }

    [SugarColumn(ColumnDataType = "text")]
    public string InputDataJson { get; set; } = "{}";

    [SugarColumn(ColumnDataType = "text")]
    public string OutputDataJson { get; set; } = "{}";

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ErrorMessage { get; set; }

    public int RetryCount { get; set; }

    public int MaxRetries { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public long? DurationMs { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? CompensationDataJson { get; set; }

    public bool IsCompensated { get; set; }

    public void Start()
    {
        Status = NodeRunStatus.Running;
        StartedAt = DateTime.UtcNow;
    }

    public void Complete(string? outputJson)
    {
        OutputDataJson = string.IsNullOrWhiteSpace(outputJson) ? "{}" : outputJson!;
        Status = NodeRunStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        if (StartedAt.HasValue)
            DurationMs = (long)(CompletedAt.Value - StartedAt.Value).TotalMilliseconds;
    }

    public void Fail(string errorMessage, bool canRetry)
    {
        ErrorMessage = errorMessage;
        if (canRetry && RetryCount < MaxRetries)
            Status = NodeRunStatus.WaitingForRetry;
        else
        {
            Status = NodeRunStatus.Failed;
            CompletedAt = DateTime.UtcNow;
            if (StartedAt.HasValue)
                DurationMs = (long)(CompletedAt.Value - StartedAt.Value).TotalMilliseconds;
        }
    }

    public void Retry()
    {
        if (Status != NodeRunStatus.WaitingForRetry)
            return;
        RetryCount++;
        Status = NodeRunStatus.Pending;
        ErrorMessage = null;
        StartedAt = null;
        CompletedAt = null;
        DurationMs = null;
    }

    public void Skip()
    {
        Status = NodeRunStatus.Skipped;
        CompletedAt = DateTime.UtcNow;
        if (StartedAt.HasValue)
            DurationMs = (long)(CompletedAt.Value - StartedAt.Value).TotalMilliseconds;
    }

    public void Compensate(string? compensationData)
    {
        CompensationDataJson = compensationData;
        Status = NodeRunStatus.Compensating;
    }
}

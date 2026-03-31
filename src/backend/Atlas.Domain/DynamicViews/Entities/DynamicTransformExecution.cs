using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.DynamicViews.Entities;

[SugarTable("DynamicTransformExecution")]
public sealed class DynamicTransformExecution : TenantEntity
{
    public DynamicTransformExecution() : base(TenantId.Empty)
    {
        JobKey = string.Empty;
        Status = "Succeeded";
        TriggerType = "manual";
        InputRows = 0;
        OutputRows = 0;
        FailedRows = 0;
        DurationMs = 0;
        ErrorDetailJson = null;
        StartedBy = 0;
        Message = null;
        StartedAt = DateTimeOffset.MinValue;
        EndedAt = null;
        AppId = null;
    }

    public DynamicTransformExecution(
        TenantId tenantId,
        long id,
        long? appId,
        string jobKey,
        string status,
        string triggerType,
        int inputRows,
        int outputRows,
        int failedRows,
        long durationMs,
        string? errorDetailJson,
        long startedBy,
        DateTimeOffset startedAt,
        DateTimeOffset? endedAt,
        string? message) : base(tenantId)
    {
        Id = id;
        AppId = appId;
        JobKey = jobKey;
        Status = status;
        TriggerType = triggerType;
        InputRows = inputRows;
        OutputRows = outputRows;
        FailedRows = failedRows;
        DurationMs = durationMs;
        ErrorDetailJson = errorDetailJson;
        StartedBy = startedBy;
        StartedAt = startedAt;
        EndedAt = endedAt;
        Message = message;
    }

    [SugarColumn(IsNullable = true)]
    public long? AppId { get; private set; }

    public string JobKey { get; private set; }

    public string Status { get; private set; }

    public string TriggerType { get; private set; }

    public int InputRows { get; private set; }

    public int OutputRows { get; private set; }

    public int FailedRows { get; private set; }

    public long DurationMs { get; private set; }

    [SugarColumn(IsNullable = true)]
    public string? ErrorDetailJson { get; private set; }

    public long StartedBy { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    [SugarColumn(IsNullable = true)]
    public DateTimeOffset? EndedAt { get; private set; }

    [SugarColumn(IsNullable = true)]
    public string? Message { get; private set; }
}

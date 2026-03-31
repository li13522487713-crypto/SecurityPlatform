using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.DynamicViews.Entities;

[SugarTable("DynamicTransformJob")]
public sealed class DynamicTransformJob : TenantEntity
{
    public DynamicTransformJob() : base(TenantId.Empty)
    {
        JobKey = string.Empty;
        Name = string.Empty;
        DefinitionJson = "{}";
        Status = "Draft";
        CronExpression = null;
        Enabled = false;
        LastRunAt = null;
        LastRunStatus = null;
        LastError = null;
        SourceConfigJson = "{}";
        TargetConfigJson = "{}";
        AppId = null;
        CreatedAt = DateTimeOffset.MinValue;
        UpdatedAt = DateTimeOffset.MinValue;
        CreatedBy = 0;
        UpdatedBy = 0;
    }

    public DynamicTransformJob(
        TenantId tenantId,
        long id,
        long? appId,
        string jobKey,
        string name,
        string definitionJson,
        long createdBy,
        DateTimeOffset now) : base(tenantId)
    {
        Id = id;
        AppId = appId;
        JobKey = jobKey;
        Name = name;
        DefinitionJson = definitionJson;
        Status = "Draft";
        CronExpression = null;
        Enabled = false;
        LastRunAt = null;
        LastRunStatus = null;
        LastError = null;
        SourceConfigJson = "{}";
        TargetConfigJson = "{}";
        CreatedAt = now;
        UpdatedAt = now;
        CreatedBy = createdBy;
        UpdatedBy = createdBy;
    }

    [SugarColumn(IsNullable = true)]
    public long? AppId { get; private set; }

    public string JobKey { get; private set; }

    public string Name { get; private set; }

    public string DefinitionJson { get; private set; }

    public string Status { get; private set; }

    [SugarColumn(IsNullable = true)]
    public string? CronExpression { get; private set; }

    public bool Enabled { get; private set; }

    [SugarColumn(IsNullable = true)]
    public DateTimeOffset? LastRunAt { get; private set; }

    [SugarColumn(IsNullable = true)]
    public string? LastRunStatus { get; private set; }

    [SugarColumn(IsNullable = true)]
    public string? LastError { get; private set; }

    public string SourceConfigJson { get; private set; }

    public string TargetConfigJson { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public long CreatedBy { get; private set; }

    public long UpdatedBy { get; private set; }

    public void MarkRunning(long userId, DateTimeOffset now)
    {
        Status = "Running";
        UpdatedAt = now;
        UpdatedBy = userId;
    }

    public void MarkPaused(long userId, DateTimeOffset now)
    {
        Status = "Paused";
        Enabled = false;
        UpdatedAt = now;
        UpdatedBy = userId;
    }

    public void UpdateDefinition(
        string name,
        string definitionJson,
        string sourceConfigJson,
        string targetConfigJson,
        string? cronExpression,
        bool enabled,
        long userId,
        DateTimeOffset now)
    {
        Name = name;
        DefinitionJson = string.IsNullOrWhiteSpace(definitionJson) ? "{}" : definitionJson;
        SourceConfigJson = string.IsNullOrWhiteSpace(sourceConfigJson) ? "{}" : sourceConfigJson;
        TargetConfigJson = string.IsNullOrWhiteSpace(targetConfigJson) ? "{}" : targetConfigJson;
        CronExpression = string.IsNullOrWhiteSpace(cronExpression) ? null : cronExpression.Trim();
        Enabled = enabled;
        Status = enabled ? "Ready" : "Paused";
        UpdatedAt = now;
        UpdatedBy = userId;
    }

    public void MarkResumed(long userId, DateTimeOffset now)
    {
        Enabled = true;
        Status = "Ready";
        UpdatedAt = now;
        UpdatedBy = userId;
    }

    public void MarkRunCompleted(long userId, DateTimeOffset now, string status, string? lastError)
    {
        LastRunAt = now;
        LastRunStatus = status;
        LastError = string.IsNullOrWhiteSpace(lastError) ? null : lastError;
        Status = Enabled ? "Ready" : "Paused";
        UpdatedAt = now;
        UpdatedBy = userId;
    }
}

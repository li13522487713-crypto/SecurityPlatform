using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Enums;
using SqlSugar;

namespace Atlas.Domain.DynamicTables.Entities;

/// <summary>
/// 数据模型变更任务，跟踪结构变更的全生命周期
/// </summary>
public sealed class SchemaChangeTask : TenantEntity
{
    public SchemaChangeTask()
        : base(TenantId.Empty)
    {
        AppInstanceId = 0;
        DraftIds = string.Empty;
        CurrentState = SchemaChangeTaskStatus.Pending;
        IsHighRisk = false;
        ValidationResult = null;
        AffectedResourcesSummary = null;
        ErrorMessage = null;
        RollbackInfo = null;
        Operator = 0;
        StartedAt = DateTimeOffset.MinValue;
        EndedAt = null;
    }

    public SchemaChangeTask(
        TenantId tenantId,
        long appInstanceId,
        string draftIds,
        bool isHighRisk,
        long operatorUserId,
        long id,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        AppInstanceId = appInstanceId;
        DraftIds = draftIds;
        CurrentState = SchemaChangeTaskStatus.Pending;
        IsHighRisk = isHighRisk;
        ValidationResult = null;
        AffectedResourcesSummary = null;
        ErrorMessage = null;
        RollbackInfo = null;
        Operator = operatorUserId;
        StartedAt = now;
        EndedAt = null;
    }

    public long AppInstanceId { get; private set; }

    /// <summary>关联的草稿 ID 列表（JSON 存储）</summary>
    [SugarColumn(ColumnDataType = "text")]
    public string DraftIds { get; private set; }

    public SchemaChangeTaskStatus CurrentState { get; private set; }
    public bool IsHighRisk { get; private set; }

    [SugarColumn(IsNullable = true, ColumnDataType = "text")]
    public string? ValidationResult { get; private set; }

    [SugarColumn(IsNullable = true, ColumnDataType = "text")]
    public string? AffectedResourcesSummary { get; private set; }

    [SugarColumn(IsNullable = true)]
    public string? ErrorMessage { get; private set; }

    [SugarColumn(IsNullable = true, ColumnDataType = "text")]
    public string? RollbackInfo { get; private set; }

    public long Operator { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }

    [SugarColumn(IsNullable = true)]
    public DateTimeOffset? EndedAt { get; private set; }

    public void StartValidating()
    {
        CurrentState = SchemaChangeTaskStatus.Validating;
    }

    public void MarkWaitingApproval()
    {
        CurrentState = SchemaChangeTaskStatus.WaitingApproval;
    }

    public void SetValidationResult(string validationResultJson, string? affectedResourcesJson)
    {
        ValidationResult = validationResultJson;
        AffectedResourcesSummary = affectedResourcesJson;
    }

    public void StartApplying()
    {
        CurrentState = SchemaChangeTaskStatus.Applying;
    }

    public void MarkApplied(DateTimeOffset now)
    {
        CurrentState = SchemaChangeTaskStatus.Applied;
        EndedAt = now;
    }

    public void MarkFailed(string errorMessage, DateTimeOffset now)
    {
        CurrentState = SchemaChangeTaskStatus.Failed;
        ErrorMessage = errorMessage;
        EndedAt = now;
    }

    public void MarkRolledBack(string rollbackInfo, DateTimeOffset now)
    {
        CurrentState = SchemaChangeTaskStatus.RolledBack;
        RollbackInfo = rollbackInfo;
        EndedAt = now;
    }

    public void Cancel(DateTimeOffset now)
    {
        CurrentState = SchemaChangeTaskStatus.Cancelled;
        EndedAt = now;
    }
}

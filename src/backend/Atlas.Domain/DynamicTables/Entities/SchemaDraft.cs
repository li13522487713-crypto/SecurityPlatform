using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Enums;
using SqlSugar;

namespace Atlas.Domain.DynamicTables.Entities;

/// <summary>
/// 数据模型草稿，记录尚未发布的结构变更
/// </summary>
public sealed class SchemaDraft : TenantEntity
{
    public SchemaDraft()
        : base(TenantId.Empty)
    {
        AppInstanceId = 0;
        ObjectType = SchemaDraftObjectType.Table;
        ObjectId = string.Empty;
        ObjectKey = string.Empty;
        ChangeType = SchemaDraftChangeType.Create;
        BeforeSnapshot = null;
        AfterSnapshot = null;
        RiskLevel = SchemaDraftRiskLevel.Low;
        Status = SchemaDraftStatus.Pending;
        ValidationMessage = null;
        CreatedAt = DateTimeOffset.MinValue;
        CreatedBy = 0;
    }

    public SchemaDraft(
        TenantId tenantId,
        long appInstanceId,
        SchemaDraftObjectType objectType,
        string objectId,
        string objectKey,
        SchemaDraftChangeType changeType,
        string? beforeSnapshot,
        string? afterSnapshot,
        SchemaDraftRiskLevel riskLevel,
        long createdBy,
        long id,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        AppInstanceId = appInstanceId;
        ObjectType = objectType;
        ObjectId = objectId;
        ObjectKey = objectKey;
        ChangeType = changeType;
        BeforeSnapshot = beforeSnapshot;
        AfterSnapshot = afterSnapshot;
        RiskLevel = riskLevel;
        Status = SchemaDraftStatus.Pending;
        ValidationMessage = null;
        CreatedAt = now;
        CreatedBy = createdBy;
    }

    public long AppInstanceId { get; private set; }
    public SchemaDraftObjectType ObjectType { get; private set; }

    /// <summary>对象的 ID（如 tableId）</summary>
    public string ObjectId { get; private set; }

    /// <summary>对象的业务 key（如 tableKey）</summary>
    public string ObjectKey { get; private set; }

    public SchemaDraftChangeType ChangeType { get; private set; }

    [SugarColumn(IsNullable = true, ColumnDataType = "text")]
    public string? BeforeSnapshot { get; private set; }

    [SugarColumn(IsNullable = true, ColumnDataType = "text")]
    public string? AfterSnapshot { get; private set; }

    public SchemaDraftRiskLevel RiskLevel { get; private set; }
    public SchemaDraftStatus Status { get; private set; }

    [SugarColumn(IsNullable = true)]
    public string? ValidationMessage { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public long CreatedBy { get; private set; }

    public void MarkValidated()
    {
        Status = SchemaDraftStatus.Validated;
        ValidationMessage = null;
    }

    public void MarkValidationFailed(string message)
    {
        Status = SchemaDraftStatus.Pending;
        ValidationMessage = message;
    }

    public void MarkPublished()
    {
        Status = SchemaDraftStatus.Published;
    }

    public void Abandon()
    {
        Status = SchemaDraftStatus.Abandoned;
    }
}

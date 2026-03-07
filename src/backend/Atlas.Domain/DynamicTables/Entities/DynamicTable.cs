using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Enums;

namespace Atlas.Domain.DynamicTables.Entities;

public sealed class DynamicTable : TenantEntity
{
    public DynamicTable()
        : base(TenantId.Empty)
    {
        TableKey = string.Empty;
        DisplayName = string.Empty;
        Description = null;
        DbType = DynamicDbType.Sqlite;
        Status = DynamicTableStatus.Draft;
        CreatedAt = DateTimeOffset.MinValue;
        UpdatedAt = DateTimeOffset.MinValue;
        CreatedBy = 0;
        UpdatedBy = 0;
        ApprovalFlowDefinitionId = null;
        ApprovalStatusField = null;
        AppId = null;
    }

    public DynamicTable(
        TenantId tenantId,
        string tableKey,
        string displayName,
        string? description,
        DynamicDbType dbType,
        long createdBy,
        long id,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        TableKey = tableKey;
        DisplayName = displayName;
        Description = description;
        DbType = dbType;
        Status = DynamicTableStatus.Active;
        CreatedAt = now;
        UpdatedAt = now;
        CreatedBy = createdBy;
        UpdatedBy = createdBy;
        AppId = null;
    }

    public string TableKey { get; private set; }
    public string DisplayName { get; private set; }
    public string? Description { get; private set; }
    public DynamicDbType DbType { get; private set; }
    public DynamicTableStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long CreatedBy { get; private set; }
    public long UpdatedBy { get; private set; }
    public long? AppId { get; private set; }

    /// <summary>关联的审批流定义 ID（null 表示未绑定审批流）</summary>
    public long? ApprovalFlowDefinitionId { get; private set; }

    /// <summary>审批状态字段名（动态表中用于记录审批状态的字段，如 "status"）</summary>
    public string? ApprovalStatusField { get; private set; }

    public void UpdateMeta(
        string displayName,
        string? description,
        DynamicTableStatus status,
        long updatedBy,
        DateTimeOffset now)
    {
        DisplayName = displayName;
        Description = description;
        Status = status;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    /// <summary>
    /// 绑定审批流定义
    /// </summary>
    public void BindApprovalFlow(
        long approvalFlowDefinitionId,
        string approvalStatusField,
        long updatedBy,
        DateTimeOffset now)
    {
        ApprovalFlowDefinitionId = approvalFlowDefinitionId;
        ApprovalStatusField = approvalStatusField;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    /// <summary>
    /// 解除审批流绑定
    /// </summary>
    public void UnbindApprovalFlow(long updatedBy, DateTimeOffset now)
    {
        ApprovalFlowDefinitionId = null;
        ApprovalStatusField = null;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void BindAppScope(long? appId, long updatedBy, DateTimeOffset now)
    {
        AppId = appId;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }
}

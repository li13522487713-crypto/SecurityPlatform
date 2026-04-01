using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.DynamicTables.Entities;

/// <summary>
/// 动态表多操作级别审批绑定（每个动作可绑定不同的审批流）
/// </summary>
public sealed class DynamicTableApprovalBinding : TenantEntity
{
    public DynamicTableApprovalBinding()
        : base(TenantId.Empty)
    {
        TableId = 0;
        TableKey = string.Empty;
        UpdatedAt = DateTimeOffset.MinValue;
        UpdatedBy = 0;
    }

    public DynamicTableApprovalBinding(
        TenantId tenantId,
        long tableId,
        string tableKey,
        long updatedBy,
        long id,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        TableId = tableId;
        TableKey = tableKey;
        UpdatedAt = now;
        UpdatedBy = updatedBy;
    }

    public long TableId { get; private set; }
    public string TableKey { get; private set; }

    /// <summary>创建操作绑定的审批流定义 ID（null 表示不需要审批）</summary>
    [SugarColumn(IsNullable = true)]
    public long? CreateFlowId { get; private set; }

    /// <summary>更新操作绑定的审批流定义 ID</summary>
    [SugarColumn(IsNullable = true)]
    public long? UpdateFlowId { get; private set; }

    /// <summary>删除操作绑定的审批流定义 ID</summary>
    [SugarColumn(IsNullable = true)]
    public long? DeleteFlowId { get; private set; }

    /// <summary>提交操作绑定的审批流定义 ID</summary>
    [SugarColumn(IsNullable = true)]
    public long? SubmitFlowId { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
    public long UpdatedBy { get; private set; }

    public void Update(
        long? createFlowId,
        long? updateFlowId,
        long? deleteFlowId,
        long? submitFlowId,
        long updatedBy,
        DateTimeOffset now)
    {
        CreateFlowId = createFlowId;
        UpdateFlowId = updateFlowId;
        DeleteFlowId = deleteFlowId;
        SubmitFlowId = submitFlowId;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public int BoundActionCount()
    {
        var count = 0;
        if (CreateFlowId.HasValue) count++;
        if (UpdateFlowId.HasValue) count++;
        if (DeleteFlowId.HasValue) count++;
        if (SubmitFlowId.HasValue) count++;
        return count;
    }
}

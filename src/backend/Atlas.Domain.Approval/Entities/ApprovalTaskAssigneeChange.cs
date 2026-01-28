using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Domain.Approval.Entities;

/// <summary>
/// 审批任务加签/减签记录
/// </summary>
public sealed class ApprovalTaskAssigneeChange : TenantEntity
{
    public ApprovalTaskAssigneeChange()
        : base(TenantId.Empty)
    {
        NodeId = string.Empty;
        AssigneeValue = string.Empty;
        Comment = null;
    }

    public ApprovalTaskAssigneeChange(
        TenantId tenantId,
        long instanceId,
        string nodeId,
        string assigneeValue,
        AssigneeChangeType changeType,
        long changedByUserId,
        long id,
        long? taskId = null,
        string? comment = null)
        : base(tenantId)
    {
        Id = id;
        InstanceId = instanceId;
        TaskId = taskId;
        NodeId = nodeId;
        AssigneeValue = assigneeValue;
        ChangeType = changeType;
        ChangedByUserId = changedByUserId;
        Comment = comment;
        ChangedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>任务 ID（加签时可能为空，减签时必须有）</summary>
    public long? TaskId { get; private set; }

    /// <summary>流程实例 ID</summary>
    public long InstanceId { get; private set; }

    /// <summary>节点 ID</summary>
    public string NodeId { get; private set; }

    /// <summary>审批人值（userId）</summary>
    public string AssigneeValue { get; private set; }

    /// <summary>变更类型</summary>
    public AssigneeChangeType ChangeType { get; private set; }

    /// <summary>变更操作人 ID</summary>
    public long ChangedByUserId { get; private set; }

    /// <summary>变更说明</summary>
    public string? Comment { get; private set; }

    /// <summary>变更时间</summary>
    public DateTimeOffset ChangedAt { get; private set; }
}

/// <summary>
/// 审批人变更类型
/// </summary>
public enum AssigneeChangeType
{
    /// <summary>加签</summary>
    Add = 0,

    /// <summary>减签</summary>
    Remove = 1,

    /// <summary>未来节点加签</summary>
    AddFuture = 2,

    /// <summary>未来节点减签</summary>
    RemoveFuture = 3,

    /// <summary>变更处理人</summary>
    Change = 4
}

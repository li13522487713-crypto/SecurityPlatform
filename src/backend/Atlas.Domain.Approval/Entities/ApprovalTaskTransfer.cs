using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Approval.Entities;

/// <summary>
/// 审批任务转办/委托记录
/// </summary>
public sealed class ApprovalTaskTransfer : TenantEntity
{
    public ApprovalTaskTransfer()
        : base(TenantId.Empty)
    {
        NodeId = string.Empty;
        OriginalAssigneeValue = string.Empty;
        TransferredToAssigneeValue = string.Empty;
        Comment = null;
    }

    public ApprovalTaskTransfer(
        TenantId tenantId,
        long taskId,
        long instanceId,
        string nodeId,
        string originalAssigneeValue,
        string transferredToAssigneeValue,
        long transferredByUserId,
        long id,
        string? comment = null)
        : base(tenantId)
    {
        Id = id;
        TaskId = taskId;
        InstanceId = instanceId;
        NodeId = nodeId;
        OriginalAssigneeValue = originalAssigneeValue;
        TransferredToAssigneeValue = transferredToAssigneeValue;
        TransferredByUserId = transferredByUserId;
        Comment = comment;
        TransferredAt = DateTimeOffset.UtcNow;
    }

    /// <summary>任务 ID</summary>
    public long TaskId { get; private set; }

    /// <summary>流程实例 ID</summary>
    public long InstanceId { get; private set; }

    /// <summary>节点 ID</summary>
    public string NodeId { get; private set; }

    /// <summary>原处理人值（userId）</summary>
    public string OriginalAssigneeValue { get; private set; }

    /// <summary>转办后的处理人值（userId）</summary>
    public string TransferredToAssigneeValue { get; private set; }

    /// <summary>转办操作人 ID</summary>
    public long TransferredByUserId { get; private set; }

    /// <summary>转办说明</summary>
    public string? Comment { get; private set; }

    /// <summary>转办时间</summary>
    public DateTimeOffset TransferredAt { get; private set; }
}

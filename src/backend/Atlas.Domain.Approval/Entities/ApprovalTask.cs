using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Domain.Approval.Entities;

/// <summary>
/// 审批任务（待办）
/// </summary>
public sealed class ApprovalTask : TenantEntity
{
    public ApprovalTask()
        : base(TenantId.Empty)
    {
        NodeId = string.Empty;
        Title = string.Empty;
        AssigneeValue = string.Empty;
        Comment = null;
    }

    public ApprovalTask(
        TenantId tenantId,
        long instanceId,
        string nodeId,
        string title,
        AssigneeType assigneeType,
        string assigneeValue,
        long id)
        : base(tenantId)
    {
        Id = id;
        InstanceId = instanceId;
        NodeId = nodeId;
        Title = title;
        AssigneeType = assigneeType;
        AssigneeValue = assigneeValue;
        Status = ApprovalTaskStatus.Pending;
        DecisionByUserId = null;
        DecisionAt = null;
        Comment = null;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>实例 ID</summary>
    public long InstanceId { get; private set; }

    /// <summary>节点 ID</summary>
    public string NodeId { get; private set; }

    /// <summary>任务标题</summary>
    public string Title { get; private set; }

    /// <summary>审批人分配类型</summary>
    public AssigneeType AssigneeType { get; private set; }

    /// <summary>审批人值（userId/roleCode/departmentId）</summary>
    public string AssigneeValue { get; private set; }

    /// <summary>任务状态</summary>
    public ApprovalTaskStatus Status { get; private set; }

    /// <summary>决策人 ID（审批人）</summary>
    public long? DecisionByUserId { get; private set; }

    /// <summary>决策时间</summary>
    public DateTimeOffset? DecisionAt { get; private set; }

    /// <summary>审批意见</summary>
    public string? Comment { get; private set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>原处理人值（转办前）</summary>
    public string? OriginalAssigneeValue { get; private set; }

    /// <summary>顺序号（用于顺序会签，从1开始）</summary>
    public int Order { get; private set; }

    public ApprovalTask(
        TenantId tenantId,
        long instanceId,
        string nodeId,
        string title,
        AssigneeType assigneeType,
        string assigneeValue,
        long id,
        int order = 0,
        ApprovalTaskStatus initialStatus = ApprovalTaskStatus.Pending)
        : base(tenantId)
    {
        Id = id;
        InstanceId = instanceId;
        NodeId = nodeId;
        Title = title;
        AssigneeType = assigneeType;
        AssigneeValue = assigneeValue;
        Status = initialStatus;
        DecisionByUserId = null;
        DecisionAt = null;
        Comment = null;
        CreatedAt = DateTimeOffset.UtcNow;
        Order = order;
    }

    public void Approve(long decisionByUserId, string? comment, DateTimeOffset now)
    {
        Status = ApprovalTaskStatus.Approved;
        DecisionByUserId = decisionByUserId;
        DecisionAt = now;
        Comment = comment;
    }

    public void Reject(long decisionByUserId, string? comment, DateTimeOffset now)
    {
        Status = ApprovalTaskStatus.Rejected;
        DecisionByUserId = decisionByUserId;
        DecisionAt = now;
        Comment = comment;
    }

    public void Cancel()
    {
        Status = ApprovalTaskStatus.Canceled;
    }

    public void Transfer(string newAssigneeValue)
    {
        OriginalAssigneeValue = AssigneeValue;
        AssigneeValue = newAssigneeValue;
    }

    /// <summary>
    /// 激活任务（顺序会签中使用）
    /// </summary>
    public void Activate()
    {
        if (Status == ApprovalTaskStatus.Waiting)
        {
            Status = ApprovalTaskStatus.Pending;
        }
    }
}

using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;
using SqlSugar;

namespace Atlas.Domain.Approval.Entities;

/// <summary>
/// 审批任务（待办）
/// </summary>
[SugarIndex(
    "IX_ApprovalTask_TenantId_AssigneeValue_Status",
    nameof(TenantIdValue), OrderByType.Asc,
    nameof(AssigneeValue), OrderByType.Asc,
    nameof(Status), OrderByType.Asc)]
[SugarIndex(
    "IX_ApprovalTask_TenantId_InstanceId",
    nameof(TenantIdValue), OrderByType.Asc,
    nameof(InstanceId), OrderByType.Asc)]
[SugarIndex(
    "IX_ApprovalTask_TenantId_InstanceId_NodeId",
    nameof(TenantIdValue), OrderByType.Asc,
    nameof(InstanceId), OrderByType.Asc,
    nameof(NodeId), OrderByType.Asc)]
[SugarIndex(
    "IX_ApprovalTask_TenantId_InstanceId_Status",
    nameof(TenantIdValue), OrderByType.Asc,
    nameof(InstanceId), OrderByType.Asc,
    nameof(Status), OrderByType.Asc)]
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

    /// <summary>票签权重（百分比，默认1）</summary>
    public int? Weight { get; private set; }

    /// <summary>父任务 ID（用于委派/加签追踪）</summary>
    public long? ParentTaskId { get; private set; }

    /// <summary>委派人 ID</summary>
    public long? DelegatorUserId { get; private set; }

    /// <summary>已阅时间</summary>
    public DateTimeOffset? ViewedAt { get; private set; }

    /// <summary>任务类型（0=主办 1=审批 2=抄送 10=转办 11=委派 12=委派归还 13=代理）</summary>
    public int TaskType { get; private set; }

    /// <summary>乐观并发版本号（SqlSugar 自动校验）</summary>
    public long RowVersion { get; private set; }

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
        if (Status != ApprovalTaskStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot approve task in '{Status}' status. Expected: Pending.");
        }
        Status = ApprovalTaskStatus.Approved;
        DecisionByUserId = decisionByUserId;
        DecisionAt = now;
        Comment = comment;
    }

    public void Reject(long decisionByUserId, string? comment, DateTimeOffset now)
    {
        if (Status != ApprovalTaskStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot reject task in '{Status}' status. Expected: Pending.");
        }
        Status = ApprovalTaskStatus.Rejected;
        DecisionByUserId = decisionByUserId;
        DecisionAt = now;
        Comment = comment;
    }

    public void Cancel()
    {
        if (Status == ApprovalTaskStatus.Approved || Status == ApprovalTaskStatus.Rejected)
        {
            // Already decided — skip silently to allow bulk cancel of mixed-status collections
            return;
        }
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

    public void Delegate(long delegatorUserId, string delegateeAssigneeValue)
    {
        if (Status != ApprovalTaskStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot delegate task in '{Status}' status. Expected: Pending.");
        }
        Status = ApprovalTaskStatus.Delegated;
        DelegatorUserId = delegatorUserId;
        // 注意：委派通常会创建一个新任务给被委派人，当前任务标记为已委派
        // 这里仅更新状态，新任务创建由服务层处理
    }

    public void ClaimBack()
    {
        if (Status == ApprovalTaskStatus.Delegated)
        {
            Status = ApprovalTaskStatus.Pending;
            DelegatorUserId = null;
        }
    }

    public void MarkViewed(DateTimeOffset now)
    {
        if (!ViewedAt.HasValue)
        {
            ViewedAt = now;
        }
    }

    public void SetWeight(int? weight)
    {
        Weight = weight;
    }

    public void SetTaskType(int taskType)
    {
        TaskType = taskType;
    }

    public void SetParentTaskId(long? parentTaskId)
    {
        ParentTaskId = parentTaskId;
    }
}

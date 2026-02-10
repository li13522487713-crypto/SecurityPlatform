namespace Atlas.Domain.Approval.Enums;

/// <summary>
/// 审批任务状态
/// </summary>
public enum ApprovalTaskStatus
{
    /// <summary>待审批</summary>
    Pending = 0,

    /// <summary>已同意</summary>
    Approved = 1,

    /// <summary>已驳回</summary>
    Rejected = 2,

    /// <summary>已取消</summary>
    Canceled = 3,

    /// <summary>等待激活（顺序会签中，等待前序任务完成）</summary>
    Waiting = 4,

    /// <summary>已委派</summary>
    Delegated = 5,

    /// <summary>已认领</summary>
    Claimed = 6,

    /// <summary>自动通过</summary>
    AutoApproved = 7,

    /// <summary>自动拒绝</summary>
    AutoRejected = 8
}

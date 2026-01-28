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
    Waiting = 4
}

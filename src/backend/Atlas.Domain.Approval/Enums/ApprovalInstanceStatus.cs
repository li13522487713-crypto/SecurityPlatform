namespace Atlas.Domain.Approval.Enums;

/// <summary>
/// 审批流实例状态
/// </summary>
public enum ApprovalInstanceStatus
{
    /// <summary>已作废</summary>
    Destroy = -3,

    /// <summary>已挂起/暂停</summary>
    Suspended = -2,

    /// <summary>暂存待审/草稿</summary>
    Draft = -1,

    /// <summary>运行中</summary>
    Running = 0,

    /// <summary>已完成</summary>
    Completed = 1,

    /// <summary>已驳回</summary>
    Rejected = 2,

    /// <summary>已取消</summary>
    Canceled = 3,

    /// <summary>超时结束</summary>
    TimedOut = 4,

    /// <summary>强制终止</summary>
    Terminated = 5,

    /// <summary>自动通过</summary>
    AutoApproved = 6,

    /// <summary>自动拒绝</summary>
    AutoRejected = 7,

    /// <summary>AI处理中</summary>
    AiProcessing = 8,

    /// <summary>AI转人工</summary>
    AiManualReview = 9
}

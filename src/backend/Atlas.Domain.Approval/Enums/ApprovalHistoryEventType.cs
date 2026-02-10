namespace Atlas.Domain.Approval.Enums;

/// <summary>
/// 审批历史事件类型
/// </summary>
public enum ApprovalHistoryEventType
{
    /// <summary>流程启动</summary>
    InstanceStarted = 0,

    /// <summary>任务创建</summary>
    TaskCreated = 1,

    /// <summary>任务同意</summary>
    TaskApproved = 2,

    /// <summary>任务驳回</summary>
    TaskRejected = 3,

    /// <summary>节点推进</summary>
    NodeAdvanced = 4,

    /// <summary>流程完成</summary>
    InstanceCompleted = 5,

    /// <summary>流程驳回</summary>
    InstanceRejected = 6,

    /// <summary>流程取消</summary>
    InstanceCanceled = 7,

    // --- Extended event types for specific operations ---

    /// <summary>任务转办</summary>
    TaskTransferred = 10,

    /// <summary>撤销同意</summary>
    DrawBackAgree = 11,

    /// <summary>流程撤回（发起人撤回）</summary>
    ProcessDrawBack = 12,

    /// <summary>退回到任意节点</summary>
    BackToAnyNode = 13,

    /// <summary>退回到修改节点</summary>
    BackToModify = 14,

    /// <summary>加签</summary>
    AssigneeAdded = 15,

    /// <summary>减签</summary>
    AssigneeRemoved = 16,

    /// <summary>改签（变更审批人）</summary>
    AssigneeChanged = 17,

    /// <summary>转发</summary>
    TaskForwarded = 18,

    /// <summary>承办</summary>
    TaskUndertaken = 19,

    /// <summary>流程推进（管理员跳过）</summary>
    ProcessMoveAhead = 20,

    /// <summary>恢复到历史状态</summary>
    RecoverToHistory = 21,

    /// <summary>保存草稿</summary>
    DraftSaved = 22,

    /// <summary>任务委派</summary>
    TaskDelegated = 23,

    /// <summary>委派归还</summary>
    TaskDelegateReturned = 24,

    /// <summary>任务认领</summary>
    TaskClaimed = 25,

    /// <summary>任务跳转</summary>
    TaskJumped = 26,

    /// <summary>任务拿回</summary>
    TaskReclaimed = 27,

    /// <summary>任务唤醒</summary>
    TaskResumed = 28,

    /// <summary>流程挂起</summary>
    InstanceSuspended = 29,

    /// <summary>流程激活</summary>
    InstanceActivated = 30,

    /// <summary>流程强制终止</summary>
    InstanceTerminated = 31,

    /// <summary>任务催办</summary>
    TaskUrged = 32,

    /// <summary>任务沟通</summary>
    TaskCommunicated = 33
}

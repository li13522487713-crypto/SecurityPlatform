namespace Atlas.Domain.Approval.Enums;

/// <summary>
/// 审批流程消息通知事件类型（对应 AntFlow 的 MsgProcessEventEnum）
/// </summary>
public enum ApprovalNotificationEventType
{
    /// <summary>流程启动</summary>
    InstanceStarted = 1,

    /// <summary>任务创建（待办通知）</summary>
    TaskCreated = 2,

    /// <summary>任务同意</summary>
    TaskApproved = 3,

    /// <summary>任务驳回</summary>
    TaskRejected = 4,

    /// <summary>流程完成</summary>
    InstanceCompleted = 5,

    /// <summary>流程驳回</summary>
    InstanceRejected = 6,

    /// <summary>流程取消</summary>
    InstanceCanceled = 7,

    /// <summary>任务转办</summary>
    TaskTransferred = 8,

    /// <summary>任务加签</summary>
    TaskAssigneeAdded = 9,

    /// <summary>任务减签</summary>
    TaskAssigneeRemoved = 10,

    /// <summary>流程撤回</summary>
    ProcessWithdrawn = 11,

    /// <summary>打回修改</summary>
    BackToModify = 12,

    /// <summary>退回任意节点</summary>
    BackToAnyNode = 13,

    /// <summary>抄送通知</summary>
    CopySent = 14,

    /// <summary>催办提醒</summary>
    Reminder = 15,

    /// <summary>超时提醒</summary>
    TimeoutReminder = 16
}

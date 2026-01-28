namespace Atlas.Domain.Approval.Enums;

/// <summary>
/// 外部回调事件类型
/// </summary>
public enum CallbackEventType
{
    /// <summary>流程启动</summary>
    InstanceStarted = 1,

    /// <summary>流程完成</summary>
    InstanceCompleted = 2,

    /// <summary>流程驳回</summary>
    InstanceRejected = 3,

    /// <summary>流程取消</summary>
    InstanceCanceled = 4,

    /// <summary>任务创建</summary>
    TaskCreated = 5,

    /// <summary>任务同意</summary>
    TaskApproved = 6,

    /// <summary>任务驳回</summary>
    TaskRejected = 7,

    /// <summary>节点完成</summary>
    NodeCompleted = 8
}

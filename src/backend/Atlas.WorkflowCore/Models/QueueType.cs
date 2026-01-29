namespace Atlas.WorkflowCore.Models;

/// <summary>
/// 队列类型枚举
/// </summary>
public enum QueueType
{
    /// <summary>
    /// 工作流队列 - 用于工作流实例执行
    /// </summary>
    Workflow = 0,

    /// <summary>
    /// 事件队列 - 用于事件处理
    /// </summary>
    Event = 1,

    /// <summary>
    /// 索引队列 - 用于搜索索引更新
    /// </summary>
    Index = 2
}

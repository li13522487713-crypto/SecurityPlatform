namespace Atlas.WorkflowCore.Models.LifeCycleEvents;

/// <summary>
/// 工作流生命周期事件基类
/// </summary>
public abstract class LifeCycleEvent
{
    /// <summary>
    /// 事件时间
    /// </summary>
    public DateTime EventTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 引用对象（工作流实例ID或其他标识）
    /// </summary>
    public object Reference { get; set; } = null!;
}

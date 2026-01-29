namespace Atlas.WorkflowCore.Models.LifeCycleEvents;

/// <summary>
/// 工作流终止事件
/// </summary>
public class WorkflowTerminated : LifeCycleEvent
{
    /// <summary>
    /// 工作流实例ID
    /// </summary>
    public string WorkflowInstanceId { get; set; } = string.Empty;

    /// <summary>
    /// 工作流定义ID
    /// </summary>
    public string WorkflowDefinitionId { get; set; } = string.Empty;

    /// <summary>
    /// 版本
    /// </summary>
    public int Version { get; set; }
}

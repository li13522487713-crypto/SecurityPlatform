namespace Atlas.WorkflowCore.Models.LifeCycleEvents;

/// <summary>
/// 步骤完成事件
/// </summary>
public class StepCompleted : LifeCycleEvent
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

    /// <summary>
    /// 步骤ID
    /// </summary>
    public int StepId { get; set; }

    /// <summary>
    /// 步骤名称
    /// </summary>
    public string StepName { get; set; } = string.Empty;

    /// <summary>
    /// 执行指针ID
    /// </summary>
    public string ExecutionPointerId { get; set; } = string.Empty;
}

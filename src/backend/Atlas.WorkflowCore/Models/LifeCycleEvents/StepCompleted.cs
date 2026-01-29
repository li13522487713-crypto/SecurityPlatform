namespace Atlas.WorkflowCore.Models.LifeCycleEvents;

/// <summary>
/// 步骤完成事件
/// </summary>
public class StepCompleted : LifeCycleEvent
{
    /// <summary>
    /// 步骤ID
    /// </summary>
    public int StepId { get; set; }

    /// <summary>
    /// 执行指针ID
    /// </summary>
    public string ExecutionPointerId { get; set; } = string.Empty;
}

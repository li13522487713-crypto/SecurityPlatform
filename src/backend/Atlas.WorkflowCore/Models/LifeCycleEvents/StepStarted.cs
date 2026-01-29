namespace Atlas.WorkflowCore.Models.LifeCycleEvents;

/// <summary>
/// 步骤开始事件
/// </summary>
public class StepStarted : LifeCycleEvent
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

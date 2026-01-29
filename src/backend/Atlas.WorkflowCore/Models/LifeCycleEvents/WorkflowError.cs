namespace Atlas.WorkflowCore.Models.LifeCycleEvents;

/// <summary>
/// 工作流错误事件
/// </summary>
public class WorkflowError : LifeCycleEvent
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
    /// 步骤ID（可选，如果错误发生在特定步骤）
    /// </summary>
    public int? StepId { get; set; }

    /// <summary>
    /// 执行指针ID（可选）
    /// </summary>
    public string? ExecutionPointerId { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 异常对象
    /// </summary>
    public Exception? Exception { get; set; }
}

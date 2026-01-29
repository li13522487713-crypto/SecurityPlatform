namespace Atlas.WorkflowCore.Models;

/// <summary>
/// 工作流引擎选项配置
/// </summary>
public class WorkflowOptions
{
    /// <summary>
    /// 轮询间隔（默认10秒）
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 空闲时间（默认100毫秒，对齐开源版本）
    /// </summary>
    public TimeSpan IdleTime { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// 错误重试间隔（默认60秒，对齐开源版本）
    /// </summary>
    public TimeSpan ErrorRetryInterval { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 最大并发工作流数（默认CPU核心数或4，对齐开源版本）
    /// </summary>
    public int MaxConcurrentWorkflows { get; set; } = Math.Max(Environment.ProcessorCount, 4);

    /// <summary>
    /// 是否启用工作流处理（默认true）
    /// </summary>
    public bool EnableWorkflows { get; set; } = true;

    /// <summary>
    /// 是否启用事件处理（默认true）
    /// </summary>
    public bool EnableEvents { get; set; } = true;

    /// <summary>
    /// 是否启用索引处理（默认true）
    /// </summary>
    public bool EnableIndexes { get; set; } = true;

    /// <summary>
    /// 是否启用轮询（默认true）
    /// </summary>
    public bool EnablePolling { get; set; } = true;

    /// <summary>
    /// 是否启用生命周期事件发布（默认true）
    /// </summary>
    public bool EnableLifeCycleEventsPublisher { get; set; } = true;

    /// <summary>
    /// 默认错误处理策略
    /// </summary>
    public WorkflowErrorHandling DefaultErrorBehavior { get; set; } = WorkflowErrorHandling.Retry;
}

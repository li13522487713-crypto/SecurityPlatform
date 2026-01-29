namespace Atlas.Application.Workflow.Models;

/// <summary>
/// 执行指针响应（步骤级监控）
/// </summary>
public class ExecutionPointerResponse
{
    /// <summary>
    /// 执行指针ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 步骤ID
    /// </summary>
    public int StepId { get; set; }

    /// <summary>
    /// 步骤名称
    /// </summary>
    public string StepName { get; set; } = string.Empty;

    /// <summary>
    /// 是否活跃
    /// </summary>
    public bool Active { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 状态（Running, Complete, Failed, Sleeping, WaitingForEvent）
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 睡眠到时间
    /// </summary>
    public DateTime? SleepUntil { get; set; }

    /// <summary>
    /// 等待的事件名称
    /// </summary>
    public string? EventName { get; set; }

    /// <summary>
    /// 等待的事件键
    /// </summary>
    public string? EventKey { get; set; }
}

using System;

namespace Atlas.WorkflowCore.Models.LifeCycleEvents;

/// <summary>
/// 工作流生命周期事件基类
/// </summary>
public abstract class LifeCycleEvent
{
    /// <summary>
    /// 事件时间（UTC）
    /// </summary>
    public DateTime EventTimeUtc { get; set; }

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
    /// 引用对象（用户自定义标识）
    /// </summary>
    public string? Reference { get; set; }
}

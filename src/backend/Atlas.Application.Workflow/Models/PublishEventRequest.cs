namespace Atlas.Application.Workflow.Models;

/// <summary>
/// 发布外部事件请求
/// </summary>
public class PublishEventRequest
{
    /// <summary>
    /// 事件名称
    /// </summary>
    public string EventName { get; set; } = string.Empty;

    /// <summary>
    /// 事件键
    /// </summary>
    public string EventKey { get; set; } = string.Empty;

    /// <summary>
    /// 事件数据（可选）
    /// </summary>
    public object? EventData { get; set; }
}

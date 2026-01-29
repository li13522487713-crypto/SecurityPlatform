namespace Atlas.Application.Workflow.Models;

/// <summary>
/// 工作流实例列表项
/// </summary>
public class WorkflowInstanceListItem
{
    /// <summary>
    /// 实例ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 工作流定义ID
    /// </summary>
    public string WorkflowDefinitionId { get; set; } = string.Empty;

    /// <summary>
    /// 版本号
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// 引用标识
    /// </summary>
    public string? Reference { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTime? CompleteTime { get; set; }
}

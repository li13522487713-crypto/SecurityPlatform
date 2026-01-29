namespace Atlas.Application.Workflow.Models;

/// <summary>
/// 工作流实例详细响应
/// </summary>
public class WorkflowInstanceResponse
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
    /// 描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 引用标识
    /// </summary>
    public string? Reference { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 工作流数据（JSON字符串）
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTime? CompleteTime { get; set; }

    /// <summary>
    /// 下次执行时间（Unix时间戳毫秒）
    /// </summary>
    public long? NextExecution { get; set; }

    /// <summary>
    /// 执行指针数量
    /// </summary>
    public int ExecutionPointersCount { get; set; }
}

namespace Atlas.Application.Workflow.Models;

/// <summary>
/// 启动工作流请求
/// </summary>
public class StartWorkflowRequest
{
    /// <summary>
    /// 工作流定义ID
    /// </summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>
    /// 版本号（可选，默认使用最新版本）
    /// </summary>
    public int? Version { get; set; }

    /// <summary>
    /// 工作流数据（JSON对象）
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// 引用标识（可选）
    /// </summary>
    public string? Reference { get; set; }
}

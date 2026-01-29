namespace Atlas.Application.Workflow.Models;

/// <summary>
/// 注册工作流定义请求
/// </summary>
public class RegisterWorkflowDefinitionRequest
{
    /// <summary>
    /// 工作流ID
    /// </summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>
    /// 版本号
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// 工作流定义JSON（DSL v1 格式）
    /// </summary>
    public string DefinitionJson { get; set; } = string.Empty;

    /// <summary>
    /// 数据类型（可选，默认为 object）
    /// </summary>
    public string? DataType { get; set; }
}

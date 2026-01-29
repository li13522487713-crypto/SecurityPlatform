namespace Atlas.Application.Workflow.Models;

/// <summary>
/// 工作流定义响应
/// </summary>
public class WorkflowDefinitionResponse
{
    /// <summary>
    /// 工作流ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 版本号
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 数据类型名称
    /// </summary>
    public string? DataType { get; set; }

    /// <summary>
    /// 步骤数量
    /// </summary>
    public int StepsCount { get; set; }
}

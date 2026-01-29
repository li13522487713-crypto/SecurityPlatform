namespace Atlas.WorkflowCore.Models.Search;

/// <summary>
/// 工作流搜索结果
/// </summary>
public class WorkflowSearchResult
{
    /// <summary>
    /// 工作流实例ID
    /// </summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>
    /// 工作流定义ID
    /// </summary>
    public string WorkflowDefinitionId { get; set; } = string.Empty;

    /// <summary>
    /// 版本
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 引用
    /// </summary>
    public string? Reference { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public WorkflowStatus Status { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTime? CompletionTime { get; set; }

    /// <summary>
    /// 当前步骤信息
    /// </summary>
    public List<StepInfo> CurrentSteps { get; set; } = new();

    /// <summary>
    /// 数据摘要（可选）
    /// </summary>
    public object? DataSummary { get; set; }
}

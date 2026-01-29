namespace Atlas.WorkflowCore.Models.Search;

/// <summary>
/// 工作流搜索过滤器
/// </summary>
public class SearchFilter
{
    /// <summary>
    /// 工作流定义ID
    /// </summary>
    public string? WorkflowDefinitionId { get; set; }

    /// <summary>
    /// 工作流版本
    /// </summary>
    public int? Version { get; set; }

    /// <summary>
    /// 工作流状态
    /// </summary>
    public WorkflowStatus? Status { get; set; }

    /// <summary>
    /// 创建时间起始
    /// </summary>
    public DateTime? CreateTimeFrom { get; set; }

    /// <summary>
    /// 创建时间结束
    /// </summary>
    public DateTime? CreateTimeTo { get; set; }

    /// <summary>
    /// 完成时间起始
    /// </summary>
    public DateTime? CompletionTimeFrom { get; set; }

    /// <summary>
    /// 完成时间结束
    /// </summary>
    public DateTime? CompletionTimeTo { get; set; }

    /// <summary>
    /// 引用
    /// </summary>
    public string? Reference { get; set; }

    /// <summary>
    /// 描述（模糊搜索）
    /// </summary>
    public string? Description { get; set; }
}

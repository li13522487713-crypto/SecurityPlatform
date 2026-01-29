namespace Atlas.WorkflowCore.Models.Search;

/// <summary>
/// 步骤信息 - 用于搜索结果
/// </summary>
public class StepInfo
{
    /// <summary>
    /// 步骤ID
    /// </summary>
    public int StepId { get; set; }

    /// <summary>
    /// 步骤名称
    /// </summary>
    public string StepName { get; set; } = string.Empty;

    /// <summary>
    /// 步骤状态
    /// </summary>
    public PointerStatus Status { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; }
}

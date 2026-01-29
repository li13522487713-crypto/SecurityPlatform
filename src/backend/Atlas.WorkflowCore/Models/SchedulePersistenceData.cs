namespace Atlas.WorkflowCore.Models;

/// <summary>
/// 调度持久化数据
/// </summary>
public class SchedulePersistenceData
{
    /// <summary>
    /// 是否已延迟完成
    /// </summary>
    public bool Elapsed { get; set; }

    /// <summary>
    /// 下次执行时间
    /// </summary>
    public DateTime? NextExecutionTime { get; set; }

    /// <summary>
    /// 执行次数
    /// </summary>
    public int ExecutionCount { get; set; }
}

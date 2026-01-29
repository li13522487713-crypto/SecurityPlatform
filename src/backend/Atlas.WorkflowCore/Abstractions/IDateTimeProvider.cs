namespace Atlas.WorkflowCore.Abstractions;

/// <summary>
/// 时间提供者接口 - 便于测试
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>
    /// 当前本地时间
    /// </summary>
    DateTime Now { get; }

    /// <summary>
    /// 当前UTC时间
    /// </summary>
    DateTime UtcNow { get; }
}

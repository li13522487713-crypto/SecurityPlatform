using Atlas.WorkflowCore.Abstractions;

namespace Atlas.WorkflowCore.Services;

/// <summary>
/// 时间提供者默认实现
/// </summary>
public class DateTimeProvider : IDateTimeProvider
{
    public DateTime Now => DateTime.Now;

    public DateTime UtcNow => DateTime.UtcNow;
}

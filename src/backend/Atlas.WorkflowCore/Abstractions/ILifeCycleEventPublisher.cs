using Atlas.WorkflowCore.Models.LifeCycleEvents;

namespace Atlas.WorkflowCore.Abstractions;

/// <summary>
/// 生命周期事件发布器接口
/// </summary>
public interface ILifeCycleEventPublisher
{
    /// <summary>
    /// 发布生命周期事件
    /// </summary>
    /// <param name="evt">事件对象</param>
    Task PublishNotificationAsync(LifeCycleEvent evt, CancellationToken cancellationToken = default);
}

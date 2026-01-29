using Atlas.WorkflowCore.Models.LifeCycleEvents;

namespace Atlas.WorkflowCore.Abstractions;

/// <summary>
/// 生命周期事件中心接口
/// </summary>
public interface ILifeCycleEventHub : ILifeCycleEventPublisher
{
    /// <summary>
    /// 启动生命周期事件中心
    /// </summary>
    void Start();

    /// <summary>
    /// 订阅特定类型的生命周期事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">事件处理器</param>
    void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : LifeCycleEvent;

    /// <summary>
    /// 订阅特定类型的生命周期事件（异步）
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">异步事件处理器</param>
    void Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : LifeCycleEvent;
}

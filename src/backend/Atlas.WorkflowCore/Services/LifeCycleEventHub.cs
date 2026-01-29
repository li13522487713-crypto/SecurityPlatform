using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models.LifeCycleEvents;
using Microsoft.Extensions.Logging;

namespace Atlas.WorkflowCore.Services;

/// <summary>
/// 生命周期事件中心实现
/// </summary>
public class LifeCycleEventHub : ILifeCycleEventHub
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();
    private readonly object _lock = new();
    private readonly ILogger<LifeCycleEventHub> _logger;

    public LifeCycleEventHub(ILogger<LifeCycleEventHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 订阅事件（同步）
    /// </summary>
    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : LifeCycleEvent
    {
        lock (_lock)
        {
            var eventType = typeof(TEvent);
            if (!_handlers.ContainsKey(eventType))
            {
                _handlers[eventType] = new List<Delegate>();
            }
            _handlers[eventType].Add(handler);
        }
    }

    /// <summary>
    /// 订阅事件（异步）
    /// </summary>
    public void Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : LifeCycleEvent
    {
        lock (_lock)
        {
            var eventType = typeof(TEvent);
            if (!_handlers.ContainsKey(eventType))
            {
                _handlers[eventType] = new List<Delegate>();
            }
            _handlers[eventType].Add(handler);
        }
    }

    /// <summary>
    /// 发布事件
    /// </summary>
    public async Task PublishNotificationAsync(LifeCycleEvent evt, CancellationToken cancellationToken = default)
    {
        if (evt == null)
        {
            return;
        }

        List<Delegate> handlers;
        lock (_lock)
        {
            var eventType = evt.GetType();
            if (!_handlers.TryGetValue(eventType, out var handlersForType))
            {
                return;
            }
            // 创建副本以避免在迭代时修改集合
            handlers = new List<Delegate>(handlersForType);
        }

        foreach (var handler in handlers)
        {
            try
            {
                if (handler is Func<LifeCycleEvent, Task> asyncHandler)
                {
                    await asyncHandler(evt);
                }
                else if (handler is Action<LifeCycleEvent> syncHandler)
                {
                    syncHandler(evt);
                }
                else
                {
                    // 使用反射调用泛型处理器
                    var task = handler.DynamicInvoke(evt);
                    if (task is Task taskResult)
                    {
                        await taskResult;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生命周期事件处理器执行失败: {EventType}", evt.GetType().Name);
            }
        }
    }
}

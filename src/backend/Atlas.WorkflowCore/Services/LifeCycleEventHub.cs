using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models.LifeCycleEvents;
using Microsoft.Extensions.Logging;

namespace Atlas.WorkflowCore.Services;

/// <summary>
/// 生命周期事件中心实现（同时作为发布器）
/// </summary>
public class LifeCycleEventHub : ILifeCycleEventHub, ILifeCycleEventPublisher
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();
    private readonly object _lock = new();
    private readonly ILogger<LifeCycleEventHub> _logger;
    private bool _isStarted;

    public LifeCycleEventHub(ILogger<LifeCycleEventHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 启动生命周期事件中心
    /// </summary>
    public void Start()
    {
        if (_isStarted)
        {
            return;
        }

        _isStarted = true;
        _logger.LogInformation("生命周期事件中心已启动");
    }

    /// <summary>
    /// 启动后台任务（IBackgroundTask接口要求）
    /// </summary>
    public Task Start(CancellationToken cancellationToken)
    {
        Start();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 停止后台任务（IBackgroundTask接口要求）
    /// </summary>
    public Task Stop()
    {
        _isStarted = false;
        _logger.LogInformation("生命周期事件中心已停止");
        return Task.CompletedTask;
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
    /// 发布事件（异步分发，不阻塞调用者）
    /// </summary>
    public void PublishNotification(LifeCycleEvent evt)
    {
        if (evt == null || !_isStarted)
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

        // 使用 Task.Run 异步分发事件，避免阻塞调用者
        Task.Run(() =>
        {
            foreach (var handler in handlers)
            {
                try
                {
                    if (handler is Action<LifeCycleEvent> syncHandler)
                    {
                        syncHandler(evt);
                    }
                    else
                    {
                        // 使用反射调用泛型处理器
                        handler.DynamicInvoke(evt);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "生命周期事件处理器执行失败: {EventType}", evt.GetType().Name);
                }
            }
        });
    }
}

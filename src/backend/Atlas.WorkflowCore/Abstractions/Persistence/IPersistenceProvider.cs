using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Abstractions.Persistence;

/// <summary>
/// 持久化提供者接口 - 聚合所有仓储接口
/// </summary>
public interface IPersistenceProvider : IWorkflowRepository, IEventRepository, ISubscriptionRepository, IScheduledCommandRepository
{
    /// <summary>
    /// 确保持久化存储已初始化（数据库/表结构）
    /// </summary>
    Task EnsureStoreExists(CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建新的工作流实例（Async版本，兼容现有代码）
    /// </summary>
    Task<string> CreateWorkflowAsync(WorkflowInstance workflow, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取工作流实例（Async版本，兼容现有代码）
    /// </summary>
    Task<WorkflowInstance?> GetWorkflowAsync(string workflowId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取可运行的工作流实例（Async版本，返回实例列表）
    /// </summary>
    Task<IEnumerable<WorkflowInstance>> GetRunnableInstancesAsync(DateTime asAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// 持久化工作流实例（Async版本，兼容现有代码）
    /// </summary>
    Task PersistWorkflowAsync(WorkflowInstance workflow, CancellationToken cancellationToken = default);

    /// <summary>
    /// 持久化工作流实例（带执行指针）
    /// </summary>
    Task PersistWorkflowAsync(WorkflowInstance workflow, List<ExecutionPointer> pointers, CancellationToken cancellationToken = default);

    /// <summary>
    /// 持久化工作流实例（带事件订阅，Async版本）
    /// </summary>
    Task PersistWorkflowAsync(WorkflowInstance workflow, List<EventSubscription>? subscriptions, CancellationToken cancellationToken = default);

    /// <summary>
    /// 终止工作流实例（Async版本）
    /// </summary>
    Task TerminateWorkflowAsync(string workflowId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建事件（Async版本，兼容现有代码）
    /// </summary>
    Task<string> CreateEventAsync(Event evt, CancellationToken cancellationToken = default);

    /// <summary>
    /// 标记事件已处理（Async版本）
    /// </summary>
    Task MarkEventProcessedAsync(string eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 标记事件未处理（Async版本）
    /// </summary>
    Task MarkEventUnprocessedAsync(string eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取可运行的事件ID列表（Async版本）
    /// </summary>
    Task<IEnumerable<string>> GetRunnableEventsAsync(DateTime asAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取事件（Async版本，兼容现有代码）
    /// </summary>
    Task<Event?> GetEventAsync(string eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取事件列表（Async版本，返回Event对象）
    /// </summary>
    Task<IEnumerable<Event>> GetEventsAsync(string eventName, string eventKey, DateTime? asAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建事件订阅（Async版本，兼容现有代码）
    /// </summary>
    Task<string> CreateEventSubscriptionAsync(EventSubscription subscription, CancellationToken cancellationToken = default);

    /// <summary>
    /// 终止事件订阅（Async版本）
    /// </summary>
    Task TerminateEventSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取事件订阅（Async版本）
    /// </summary>
    Task<EventSubscription?> GetEventSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取事件订阅列表（Async版本）
    /// </summary>
    Task<IEnumerable<EventSubscription>> GetEventSubscriptionsAsync(string eventName, string eventKey, DateTime? asAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// 持久化错误信息
    /// </summary>
    Task PersistErrorsAsync(IEnumerable<ExecutionError> errors, CancellationToken cancellationToken = default);
}

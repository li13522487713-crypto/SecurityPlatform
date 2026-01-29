using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Abstractions.Persistence;

public interface IPersistenceProvider : IScheduledCommandRepository
{
    /// <summary>
    /// 确保持久化存储已初始化（数据库/表结构）
    /// </summary>
    Task EnsureStoreExists(CancellationToken cancellationToken = default);

    Task<string> CreateWorkflowAsync(WorkflowInstance workflow, CancellationToken cancellationToken = default);
    Task<WorkflowInstance?> GetWorkflowAsync(string workflowId, CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkflowInstance>> GetRunnableInstancesAsync(DateTime asAt, CancellationToken cancellationToken = default);
    Task PersistWorkflowAsync(WorkflowInstance workflow, CancellationToken cancellationToken = default);
    Task PersistWorkflowAsync(WorkflowInstance workflow, List<ExecutionPointer> pointers, CancellationToken cancellationToken = default);
    Task TerminateWorkflowAsync(string workflowId, CancellationToken cancellationToken = default);
    Task<string> CreateEventAsync(Event evt, CancellationToken cancellationToken = default);
    Task MarkEventProcessedAsync(string eventId, CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> GetRunnableEventsAsync(DateTime asAt, CancellationToken cancellationToken = default);
    Task<Event?> GetEventAsync(string eventId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Event>> GetEventsAsync(string eventName, string eventKey, DateTime? asAt, CancellationToken cancellationToken = default);
    Task<string> CreateEventSubscriptionAsync(EventSubscription subscription, CancellationToken cancellationToken = default);
    Task TerminateEventSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);
    Task<EventSubscription?> GetEventSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EventSubscription>> GetEventSubscriptionsAsync(string eventName, string eventKey, DateTime? asAt, CancellationToken cancellationToken = default);
}

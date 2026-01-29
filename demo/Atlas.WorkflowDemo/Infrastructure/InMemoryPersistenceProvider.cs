using Atlas.WorkflowCore.Abstractions.Persistence;
using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowDemo.Infrastructure;

/// <summary>
/// 内存持久化提供者 - 用于Demo演示，不依赖数据库
/// </summary>
public class InMemoryPersistenceProvider : IPersistenceProvider
{
    private readonly Dictionary<string, WorkflowInstance> _workflows = new();
    private readonly Dictionary<string, Event> _events = new();
    private readonly Dictionary<string, EventSubscription> _subscriptions = new();
    private readonly List<ScheduledCommand> _scheduledCommands = new();
    private readonly object _lock = new();

    public bool SupportsScheduledCommands => true;

    // 初始化存储
    public Task EnsureStoreExists(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    // IWorkflowRepository 实现
    public Task<string> CreateNewWorkflow(WorkflowInstance workflow, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _workflows[workflow.Id] = workflow;
            return Task.FromResult(workflow.Id);
        }
    }

    public Task<string> CreateWorkflowAsync(WorkflowInstance workflow, CancellationToken cancellationToken = default)
    {
        return CreateNewWorkflow(workflow, cancellationToken);
    }

    public Task PersistWorkflow(WorkflowInstance workflow, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _workflows[workflow.Id] = workflow;
            return Task.CompletedTask;
        }
    }

    public Task PersistWorkflow(WorkflowInstance workflow, List<EventSubscription> subscriptions, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _workflows[workflow.Id] = workflow;
            foreach (var sub in subscriptions)
            {
                _subscriptions[sub.Id] = sub;
            }
            return Task.CompletedTask;
        }
    }

    public Task PersistWorkflowAsync(WorkflowInstance workflow, CancellationToken cancellationToken = default)
    {
        return PersistWorkflow(workflow, cancellationToken);
    }

    public Task PersistWorkflowAsync(WorkflowInstance workflow, List<ExecutionPointer> pointers, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _workflows[workflow.Id] = workflow;
            return Task.CompletedTask;
        }
    }

    public Task PersistWorkflowAsync(WorkflowInstance workflow, List<EventSubscription>? subscriptions, CancellationToken cancellationToken = default)
    {
        if (subscriptions == null)
            return PersistWorkflow(workflow, cancellationToken);
        return PersistWorkflow(workflow, subscriptions, cancellationToken);
    }

    public Task<IEnumerable<string>> GetRunnableInstances(DateTime asAt, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var asAtTicks = new DateTimeOffset(asAt).ToUnixTimeMilliseconds();
            var runnableIds = _workflows.Values
                .Where(w => w.Status == WorkflowStatus.Runnable && w.NextExecution <= asAtTicks)
                .Select(w => w.Id)
                .ToList();
            return Task.FromResult<IEnumerable<string>>(runnableIds);
        }
    }

    public Task<IEnumerable<WorkflowInstance>> GetRunnableInstancesAsync(DateTime asAt, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var asAtTicks = new DateTimeOffset(asAt).ToUnixTimeMilliseconds();
            var runnableInstances = _workflows.Values
                .Where(w => w.Status == WorkflowStatus.Runnable && w.NextExecution <= asAtTicks)
                .ToList();
            return Task.FromResult<IEnumerable<WorkflowInstance>>(runnableInstances);
        }
    }

    public Task<WorkflowInstance> GetWorkflowInstance(string id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_workflows.GetValueOrDefault(id)!);
        }
    }

    public Task<WorkflowInstance?> GetWorkflowAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _workflows.TryGetValue(workflowId, out var workflow);
            return Task.FromResult(workflow);
        }
    }

    public Task<IEnumerable<WorkflowInstance>> GetWorkflowInstances(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var instances = ids
                .Where(id => _workflows.ContainsKey(id))
                .Select(id => _workflows[id])
                .ToList();
            return Task.FromResult<IEnumerable<WorkflowInstance>>(instances);
        }
    }

    public Task TerminateWorkflowAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_workflows.TryGetValue(workflowId, out var workflow))
            {
                workflow.Status = WorkflowStatus.Terminated;
            }
            return Task.CompletedTask;
        }
    }

    // IEventRepository 实现
    public Task<string> CreateEvent(Event newEvent, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _events[newEvent.Id] = newEvent;
            return Task.FromResult(newEvent.Id);
        }
    }

    public Task<string> CreateEventAsync(Event evt, CancellationToken cancellationToken = default)
    {
        return CreateEvent(evt, cancellationToken);
    }

    public Task<Event> GetEvent(string id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_events.GetValueOrDefault(id)!);
        }
    }

    public Task<Event?> GetEventAsync(string eventId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _events.TryGetValue(eventId, out var evt);
            return Task.FromResult(evt);
        }
    }

    public Task<IEnumerable<string>> GetRunnableEvents(DateTime asAt, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var runnableIds = _events.Values
                .Where(e => !e.IsProcessed && e.EventTime <= asAt)
                .Select(e => e.Id)
                .ToList();
            return Task.FromResult<IEnumerable<string>>(runnableIds);
        }
    }

    public Task<IEnumerable<string>> GetRunnableEventsAsync(DateTime asAt, CancellationToken cancellationToken = default)
    {
        return GetRunnableEvents(asAt, cancellationToken);
    }

    public Task<IEnumerable<string>> GetEvents(string eventName, string eventKey, DateTime asOf, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var eventIds = _events.Values
                .Where(e => e.EventName == eventName && e.EventKey == eventKey && e.EventTime >= asOf)
                .Select(e => e.Id)
                .ToList();
            return Task.FromResult<IEnumerable<string>>(eventIds);
        }
    }

    public Task<IEnumerable<Event>> GetEventsAsync(string eventName, string eventKey, DateTime? asAt, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var events = _events.Values
                .Where(e => e.EventName == eventName && e.EventKey == eventKey)
                .Where(e => !asAt.HasValue || e.EventTime >= asAt.Value)
                .ToList();
            return Task.FromResult<IEnumerable<Event>>(events);
        }
    }

    public Task MarkEventProcessed(string id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_events.TryGetValue(id, out var evt))
            {
                evt.IsProcessed = true;
            }
            return Task.CompletedTask;
        }
    }

    public Task MarkEventProcessedAsync(string eventId, CancellationToken cancellationToken = default)
    {
        return MarkEventProcessed(eventId, cancellationToken);
    }

    public Task MarkEventUnprocessed(string id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_events.TryGetValue(id, out var evt))
            {
                evt.IsProcessed = false;
            }
            return Task.CompletedTask;
        }
    }

    public Task MarkEventUnprocessedAsync(string eventId, CancellationToken cancellationToken = default)
    {
        return MarkEventUnprocessed(eventId, cancellationToken);
    }

    // ISubscriptionRepository 实现
    public Task<string> CreateEventSubscription(EventSubscription subscription, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _subscriptions[subscription.Id] = subscription;
            return Task.FromResult(subscription.Id);
        }
    }

    public Task<string> CreateEventSubscriptionAsync(EventSubscription subscription, CancellationToken cancellationToken = default)
    {
        return CreateEventSubscription(subscription, cancellationToken);
    }

    public Task<IEnumerable<EventSubscription>> GetSubscriptions(string eventName, string eventKey, DateTime asOf, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var subs = _subscriptions.Values
                .Where(s => s.EventName == eventName && s.EventKey == eventKey && s.SubscribeAsOf <= asOf)
                .ToList();
            return Task.FromResult<IEnumerable<EventSubscription>>(subs);
        }
    }

    public Task<IEnumerable<EventSubscription>> GetEventSubscriptionsAsync(string eventName, string eventKey, DateTime? asAt, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var subs = _subscriptions.Values
                .Where(s => s.EventName == eventName && s.EventKey == eventKey)
                .Where(s => !asAt.HasValue || s.SubscribeAsOf <= asAt.Value)
                .ToList();
            return Task.FromResult<IEnumerable<EventSubscription>>(subs);
        }
    }

    public Task TerminateSubscription(string eventSubscriptionId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _subscriptions.Remove(eventSubscriptionId);
            return Task.CompletedTask;
        }
    }

    public Task TerminateEventSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        return TerminateSubscription(subscriptionId, cancellationToken);
    }

    public Task<EventSubscription> GetSubscription(string eventSubscriptionId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_subscriptions.GetValueOrDefault(eventSubscriptionId)!);
        }
    }

    public Task<EventSubscription?> GetEventSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _subscriptions.TryGetValue(subscriptionId, out var subscription);
            return Task.FromResult(subscription);
        }
    }

    public Task<EventSubscription> GetFirstOpenSubscription(string eventName, string eventKey, DateTime asOf, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var sub = _subscriptions.Values
                .Where(s => s.EventName == eventName && s.EventKey == eventKey && s.SubscribeAsOf <= asOf)
                .FirstOrDefault();
            return Task.FromResult(sub!);
        }
    }

    public Task<bool> SetSubscriptionToken(string eventSubscriptionId, string token, string workerId, DateTime expiry, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_subscriptions.TryGetValue(eventSubscriptionId, out var sub))
            {
                sub.ExternalToken = token;
                sub.ExternalWorkerId = workerId;
                sub.ExternalTokenExpiry = expiry;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }

    public Task ClearSubscriptionToken(string eventSubscriptionId, string token, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_subscriptions.TryGetValue(eventSubscriptionId, out var sub) && sub.ExternalToken == token)
            {
                sub.ExternalToken = null;
                sub.ExternalWorkerId = null;
                sub.ExternalTokenExpiry = null;
            }
            return Task.CompletedTask;
        }
    }

    // IScheduledCommandRepository 实现
    public Task ScheduleCommand(ScheduledCommand command)
    {
        lock (_lock)
        {
            _scheduledCommands.Add(command);
            return Task.CompletedTask;
        }
    }

    public Task ScheduleCommandAsync(ScheduledCommand command, CancellationToken cancellationToken = default)
    {
        return ScheduleCommand(command);
    }

    public Task ProcessCommands(DateTimeOffset asOf, Func<ScheduledCommand, Task> action, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var asOfDateTime = asOf.UtcDateTime;
            var dueCommands = _scheduledCommands.Where(c => c.ExecuteTime <= asOfDateTime).ToList();
            foreach (var command in dueCommands)
            {
                _scheduledCommands.Remove(command);
            }

            return Task.WhenAll(dueCommands.Select(action));
        }
    }

    // 持久化错误信息
    public Task PersistErrorsAsync(IEnumerable<ExecutionError> errors, CancellationToken cancellationToken = default)
    {
        // Demo中忽略错误持久化
        return Task.CompletedTask;
    }
}

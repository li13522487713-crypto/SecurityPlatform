using System.Text.Json;
using Atlas.Core.Tenancy;
using Atlas.Domain.Workflow.Entities;
using Atlas.WorkflowCore.Abstractions.Persistence;
using Atlas.WorkflowCore.Models;
using SqlSugar;

namespace Atlas.Infrastructure.Workflow;

public class SqlSugarPersistenceProvider : IPersistenceProvider
{
    private readonly ISqlSugarClient _db;
    private readonly ITenantProvider _tenantProvider;

    public SqlSugarPersistenceProvider(ISqlSugarClient db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    public async Task EnsureStoreExists(CancellationToken cancellationToken = default)
    {
        // SqlSugar 的 CodeFirst 会自动创建表，这里确保表存在
        _db.CodeFirst.InitTables(
            typeof(PersistedWorkflow),
            typeof(PersistedExecutionPointer),
            typeof(PersistedEvent),
            typeof(PersistedSubscription)
        );

        await Task.CompletedTask;
    }

    // IWorkflowRepository 实现
    public Task<string> CreateNewWorkflow(WorkflowInstance workflow, CancellationToken cancellationToken = default)
        => CreateWorkflowAsync(workflow, cancellationToken);

    public async Task<string> CreateWorkflowAsync(WorkflowInstance workflow, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var entity = new PersistedWorkflow(tenantId, workflow.WorkflowDefinitionId, workflow.Version, long.Parse(workflow.Id));
        entity.SetDescription(workflow.Description);
        entity.SetReference(workflow.Reference);
        entity.SetDataJson(workflow.Data != null ? JsonSerializer.Serialize(workflow.Data) : null);
        entity.UpdateStatus(workflow.Status);
        entity.SetCreateTime(workflow.CreateTime);
        entity.SetCompleteTime(workflow.CompleteTime);
        entity.UpdateNextExecution(workflow.NextExecution);

        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);

        // Save execution pointers
        var pointerEntities = workflow.ExecutionPointers
            .Select(pointer => CreatePointerEntity(tenantId, workflow.Id, pointer))
            .ToList();
        if (pointerEntities.Count > 0)
        {
            await _db.Insertable(pointerEntities).ExecuteCommandAsync(cancellationToken);
        }

        return workflow.Id;
    }

    private PersistedExecutionPointer CreatePointerEntity(TenantId tenantId, string workflowId, ExecutionPointer pointer)
    {
        var entity = new PersistedExecutionPointer(tenantId, workflowId, pointer.Id, pointer.StepId, 0);
        entity.MarkActive(pointer.Active);
        entity.SetSleepUntil(pointer.SleepUntil);
        entity.SetPersistenceDataJson(pointer.PersistenceData != null ? JsonSerializer.Serialize(pointer.PersistenceData) : null);
        entity.SetStartTime(pointer.StartTime);
        entity.SetEndTime(pointer.EndTime);
        entity.SetEventInfo(pointer.EventName, pointer.EventKey, pointer.EventPublished);
        entity.SetEventDataJson(pointer.EventData != null ? JsonSerializer.Serialize(pointer.EventData) : null);
        entity.SetStepName(pointer.StepName);
        entity.SetRetryCount(pointer.RetryCount);
        entity.SetContextItemJson(pointer.ContextItem != null ? JsonSerializer.Serialize(pointer.ContextItem) : null);
        entity.SetPredecessorId(pointer.PredecessorId);
        entity.SetOutcomeJson(pointer.Outcome != null ? JsonSerializer.Serialize(pointer.Outcome) : null);
        entity.UpdateStatus(pointer.Status);
        return entity;
    }

    private static WorkflowInstance BuildWorkflowInstance(
        PersistedWorkflow entity,
        IReadOnlyList<PersistedExecutionPointer> pointers)
    {
        var workflow = new WorkflowInstance
        {
            Id = entity.Id.ToString(),
            WorkflowDefinitionId = entity.WorkflowDefinitionId,
            Version = entity.Version,
            Description = entity.Description,
            Reference = entity.Reference,
            Status = entity.Status,
            Data = entity.DataJson != null ? JsonSerializer.Deserialize<object>(entity.DataJson) : null,
            CreateTime = entity.CreateTime.DateTime,
            CompleteTime = entity.CompleteTime?.DateTime,
            NextExecution = entity.NextExecution
        };

        foreach (var pointerEntity in pointers)
        {
            var pointer = new ExecutionPointer
            {
                Id = pointerEntity.PointerId,
                StepId = pointerEntity.StepId,
                Active = pointerEntity.Active,
                SleepUntil = pointerEntity.SleepUntil?.DateTime,
                PersistenceData = pointerEntity.PersistenceDataJson != null ? JsonSerializer.Deserialize<object>(pointerEntity.PersistenceDataJson) : null,
                StartTime = pointerEntity.StartTime?.DateTime,
                EndTime = pointerEntity.EndTime?.DateTime,
                EventName = pointerEntity.EventName,
                EventKey = pointerEntity.EventKey,
                EventPublished = pointerEntity.EventPublished,
                EventData = pointerEntity.EventDataJson != null ? JsonSerializer.Deserialize<object>(pointerEntity.EventDataJson) : null,
                StepName = pointerEntity.StepName,
                RetryCount = pointerEntity.RetryCount,
                ContextItem = pointerEntity.ContextItemJson != null ? JsonSerializer.Deserialize<object>(pointerEntity.ContextItemJson) : null,
                PredecessorId = pointerEntity.PredecessorId,
                Outcome = pointerEntity.OutcomeJson != null ? JsonSerializer.Deserialize<object>(pointerEntity.OutcomeJson) : null,
                Status = pointerEntity.Status
            };
            workflow.ExecutionPointers.Add(pointer);
        }

        return workflow;
    }

    public Task<WorkflowInstance> GetWorkflowInstance(string id, CancellationToken cancellationToken = default)
    {
        return GetWorkflowAsync(id, cancellationToken).ContinueWith(t => t.Result ?? throw new InvalidOperationException($"Workflow {id} not found"));
    }

    public async Task<IEnumerable<WorkflowInstance>> GetWorkflowInstances(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();
        if (idList.Count == 0)
        {
            return Array.Empty<WorkflowInstance>();
        }

        var parsedIds = idList
            .Select(id => long.TryParse(id, out var value) ? value : (long?)null)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();
        if (parsedIds.Length == 0)
        {
            return Array.Empty<WorkflowInstance>();
        }

        var tenantId = _tenantProvider.GetTenantId();
        var entities = await _db.Queryable<PersistedWorkflow>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(parsedIds, x.Id))
            .ToListAsync(cancellationToken);
        if (entities.Count == 0)
        {
            return Array.Empty<WorkflowInstance>();
        }

        var workflowIds = entities.Select(x => x.Id.ToString()).ToList();
        var workflowIdArray = workflowIds.Distinct().ToArray();
        var pointers = await _db.Queryable<PersistedExecutionPointer>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(workflowIdArray, x.WorkflowId))
            .ToListAsync(cancellationToken);
        var pointerLookup = pointers
            .GroupBy(x => x.WorkflowId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<PersistedExecutionPointer>)x.ToList());

        return entities
            .Select(entity =>
            {
                pointerLookup.TryGetValue(entity.Id.ToString(), out var pointerEntities);
                return BuildWorkflowInstance(entity, pointerEntities ?? Array.Empty<PersistedExecutionPointer>());
            })
            .ToList();
    }

    public async Task<IEnumerable<string>> GetRunnableInstances(DateTime asAt, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var entities = await _db.Queryable<PersistedWorkflow>()
            .Where(x => x.TenantIdValue == tenantId.Value 
                && x.Status == WorkflowStatus.Runnable
                && (x.NextExecution == null || x.NextExecution <= asAt.Ticks))
            .Select(x => x.Id.ToString())
            .ToListAsync(cancellationToken);

        return entities;
    }

    public Task PersistWorkflow(WorkflowInstance workflow, CancellationToken cancellationToken = default)
        => PersistWorkflowAsync(workflow, cancellationToken);

    public Task PersistWorkflow(WorkflowInstance workflow, List<EventSubscription> subscriptions, CancellationToken cancellationToken = default)
        => PersistWorkflowAsync(workflow, subscriptions, cancellationToken);

    public async Task<WorkflowInstance?> GetWorkflowAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var entity = await _db.Queryable<PersistedWorkflow>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id.ToString() == workflowId)
            .FirstAsync(cancellationToken);

        if (entity == null)
            return null;

        // Load execution pointers
        var pointers = await _db.Queryable<PersistedExecutionPointer>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.WorkflowId == workflowId)
            .ToListAsync(cancellationToken);
        return BuildWorkflowInstance(entity, pointers);
    }

    public async Task<IEnumerable<WorkflowInstance>> GetRunnableInstancesAsync(DateTime asAt, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var entities = await _db.Queryable<PersistedWorkflow>()
            .Where(x => x.TenantIdValue == tenantId.Value 
                && x.Status == WorkflowStatus.Runnable
                && (x.NextExecution == null || x.NextExecution <= asAt.Ticks))
            .ToListAsync(cancellationToken);

        if (entities.Count == 0)
        {
            return Array.Empty<WorkflowInstance>();
        }

        var workflowIds = entities.Select(x => x.Id.ToString()).ToList();
        var workflowIdArray = workflowIds.Distinct().ToArray();
        var pointers = await _db.Queryable<PersistedExecutionPointer>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(workflowIdArray, x.WorkflowId))
            .ToListAsync(cancellationToken);
        var pointerLookup = pointers
            .GroupBy(x => x.WorkflowId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<PersistedExecutionPointer>)x.ToList());

        return entities
            .Select(entity =>
            {
                pointerLookup.TryGetValue(entity.Id.ToString(), out var pointerEntities);
                return BuildWorkflowInstance(entity, pointerEntities ?? Array.Empty<PersistedExecutionPointer>());
            })
            .ToList();
    }

    public async Task PersistWorkflowAsync(WorkflowInstance workflow, CancellationToken cancellationToken = default)
    {
        await PersistWorkflowAsync(workflow, workflow.ExecutionPointers.ToList(), cancellationToken);
    }

    public async Task PersistWorkflowAsync(WorkflowInstance workflow, List<ExecutionPointer> pointers, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var entity = await _db.Queryable<PersistedWorkflow>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id.ToString() == workflow.Id)
            .FirstAsync(cancellationToken);

        if (entity != null)
        {
            entity.UpdateStatus(workflow.Status);
            entity.UpdateNextExecution(workflow.NextExecution);
            entity.SetDataJson(workflow.Data != null ? JsonSerializer.Serialize(workflow.Data) : null);
            if (workflow.CompleteTime.HasValue)
            {
                entity.SetCompleteTime(workflow.CompleteTime.Value);
            }
            await _db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        }

        // Update execution pointers
        var existingPointers = await _db.Queryable<PersistedExecutionPointer>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.WorkflowId == workflow.Id)
            .ToListAsync(cancellationToken);
        var pointerMap = existingPointers.ToDictionary(x => x.PointerId, x => x);

        var toUpdate = new List<PersistedExecutionPointer>();
        var toInsert = new List<PersistedExecutionPointer>();
        foreach (var pointer in pointers)
        {
            if (pointerMap.TryGetValue(pointer.Id, out var pointerEntity))
            {
                pointerEntity.UpdateStatus(pointer.Status);
                pointerEntity.MarkActive(pointer.Active);
                pointerEntity.SetSleepUntil(pointer.SleepUntil);
                pointerEntity.SetPersistenceDataJson(pointer.PersistenceData != null ? JsonSerializer.Serialize(pointer.PersistenceData) : null);
                pointerEntity.SetStartTime(pointer.StartTime);
                pointerEntity.SetEndTime(pointer.EndTime);
                pointerEntity.SetEventInfo(pointer.EventName, pointer.EventKey, pointer.EventPublished);
                pointerEntity.SetEventDataJson(pointer.EventData != null ? JsonSerializer.Serialize(pointer.EventData) : null);
                pointerEntity.SetRetryCount(pointer.RetryCount);
                pointerEntity.SetContextItemJson(pointer.ContextItem != null ? JsonSerializer.Serialize(pointer.ContextItem) : null);
                pointerEntity.SetOutcomeJson(pointer.Outcome != null ? JsonSerializer.Serialize(pointer.Outcome) : null);
                toUpdate.Add(pointerEntity);
            }
            else
            {
                toInsert.Add(CreatePointerEntity(tenantId, workflow.Id, pointer));
            }
        }

        if (toUpdate.Count > 0)
        {
            await _db.Updateable(toUpdate).ExecuteCommandAsync(cancellationToken);
        }

        if (toInsert.Count > 0)
        {
            await _db.Insertable(toInsert).ExecuteCommandAsync(cancellationToken);
        }
    }

    public async Task PersistWorkflowAsync(WorkflowInstance workflow, List<EventSubscription>? subscriptions, CancellationToken cancellationToken = default)
    {
        // 先持久化工作流和指针
        await PersistWorkflowAsync(workflow, workflow.ExecutionPointers.ToList(), cancellationToken);

        // 如果有订阅,持久化订阅
        if (subscriptions != null && subscriptions.Count > 0)
        {
            var tenantId = _tenantProvider.GetTenantId();
            var entities = subscriptions
                .Select(subscription => new PersistedSubscription(
                    tenantId,
                    subscription.WorkflowId,
                    subscription.StepId,
                    subscription.ExecutionPointerId,
                    subscription.EventName,
                    subscription.EventKey,
                    long.Parse(subscription.Id),
                    subscription.SubscriptionData))
                .ToList();
            await _db.Insertable(entities).ExecuteCommandAsync(cancellationToken);
        }
    }

    public async Task TerminateWorkflowAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var entity = await _db.Queryable<PersistedWorkflow>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id.ToString() == workflowId)
            .FirstAsync(cancellationToken);

        if (entity != null)
        {
            entity.UpdateStatus(WorkflowStatus.Terminated);
            await _db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        }
    }

    // IEventRepository 实现
    public Task<string> CreateEvent(Event newEvent, CancellationToken cancellationToken = default)
        => CreateEventAsync(newEvent, cancellationToken);

    public Task<Event> GetEvent(string id, CancellationToken cancellationToken = default)
    {
        return GetEventAsync(id, cancellationToken).ContinueWith(t => t.Result ?? throw new InvalidOperationException($"Event {id} not found"));
    }

    public Task<IEnumerable<string>> GetRunnableEvents(DateTime asAt, CancellationToken cancellationToken = default)
        => GetRunnableEventsAsync(asAt, cancellationToken);

    public Task<IEnumerable<string>> GetEvents(string eventName, string eventKey, DateTime asOf, CancellationToken cancellationToken = default)
    {
        return GetEventsAsync(eventName, eventKey, asOf, cancellationToken).ContinueWith(t => t.Result.Select(e => e.Id));
    }

    public Task MarkEventProcessed(string id, CancellationToken cancellationToken = default)
        => MarkEventProcessedAsync(id, cancellationToken);

    public Task MarkEventUnprocessed(string id, CancellationToken cancellationToken = default)
        => MarkEventUnprocessedAsync(id, cancellationToken);

    public async Task<string> CreateEventAsync(Event evt, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var entity = new PersistedEvent(tenantId, evt.EventName, evt.EventKey, long.Parse(evt.Id), evt.EventData != null ? JsonSerializer.Serialize(evt.EventData) : null);
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return evt.Id;
    }

    public async Task MarkEventProcessedAsync(string eventId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var entity = await _db.Queryable<PersistedEvent>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id.ToString() == eventId)
            .FirstAsync(cancellationToken);

        if (entity != null)
        {
            entity.MarkAsProcessed();
            await _db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        }
    }

    public async Task MarkEventUnprocessedAsync(string eventId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var entity = await _db.Queryable<PersistedEvent>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id.ToString() == eventId)
            .FirstAsync(cancellationToken);

        if (entity != null)
        {
            entity.MarkAsUnprocessed();
            await _db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        }
    }

    public async Task<IEnumerable<string>> GetRunnableEventsAsync(DateTime asAt, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var asAtOffset = new DateTimeOffset(asAt, TimeSpan.Zero);
        var entities = await _db.Queryable<PersistedEvent>()
            .Where(x => x.TenantIdValue == tenantId.Value && !x.IsProcessed && x.EventTime <= asAtOffset)
            .Select(x => x.Id.ToString())
            .ToListAsync(cancellationToken);

        return entities;
    }

    public async Task<Event?> GetEventAsync(string eventId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var entity = await _db.Queryable<PersistedEvent>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id.ToString() == eventId)
            .FirstAsync(cancellationToken);

        if (entity == null)
            return null;

        return new Event
        {
            Id = entity.Id.ToString(),
            EventName = entity.EventName,
            EventKey = entity.EventKey,
            EventData = entity.EventDataJson != null ? JsonSerializer.Deserialize<object>(entity.EventDataJson) : null,
            EventTime = entity.EventTime.DateTime,
            IsProcessed = entity.IsProcessed
        };
    }

    public async Task<IEnumerable<Event>> GetEventsAsync(string eventName, string eventKey, DateTime? asAt, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var query = _db.Queryable<PersistedEvent>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.EventName == eventName && x.EventKey == eventKey);

        if (asAt.HasValue)
        {
            query = query.Where(x => x.EventTime <= asAt.Value);
        }

        var entities = await query.ToListAsync(cancellationToken);

        return entities.Select(e => new Event
        {
            Id = e.Id.ToString(),
            EventName = e.EventName,
            EventKey = e.EventKey,
            EventData = e.EventDataJson != null ? JsonSerializer.Deserialize<object>(e.EventDataJson) : null,
            EventTime = e.EventTime.DateTime,
            IsProcessed = e.IsProcessed
        });
    }

    // ISubscriptionRepository 实现
    public Task<string> CreateEventSubscription(EventSubscription subscription, CancellationToken cancellationToken = default)
        => CreateEventSubscriptionAsync(subscription, cancellationToken);

    public Task<IEnumerable<EventSubscription>> GetSubscriptions(string eventName, string eventKey, DateTime asOf, CancellationToken cancellationToken = default)
        => GetEventSubscriptionsAsync(eventName, eventKey, asOf, cancellationToken);

    public Task TerminateSubscription(string eventSubscriptionId, CancellationToken cancellationToken = default)
        => TerminateEventSubscriptionAsync(eventSubscriptionId, cancellationToken);

    public Task<EventSubscription> GetSubscription(string eventSubscriptionId, CancellationToken cancellationToken = default)
    {
        return GetEventSubscriptionAsync(eventSubscriptionId, cancellationToken).ContinueWith(t => t.Result ?? throw new InvalidOperationException($"Subscription {eventSubscriptionId} not found"));
    }

    public async Task<EventSubscription> GetFirstOpenSubscription(string eventName, string eventKey, DateTime asOf, CancellationToken cancellationToken = default)
    {
        var subscriptions = await GetEventSubscriptionsAsync(eventName, eventKey, asOf, cancellationToken);
        return subscriptions.FirstOrDefault() ?? throw new InvalidOperationException($"No open subscription found for {eventName}#{eventKey}");
    }

    public Task<bool> SetSubscriptionToken(string eventSubscriptionId, string token, string workerId, DateTime expiry, CancellationToken cancellationToken = default)
    {
        // TODO: 实现订阅令牌管理
        // 需要在 PersistedSubscription 实体中添加 Token、WorkerId、Expiry 字段
        return Task.FromResult(false);
    }

    public Task ClearSubscriptionToken(string eventSubscriptionId, string token, CancellationToken cancellationToken = default)
    {
        // TODO: 实现订阅令牌清除
        return Task.CompletedTask;
    }

    public async Task<string> CreateEventSubscriptionAsync(EventSubscription subscription, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var entity = new PersistedSubscription(tenantId, subscription.WorkflowId, subscription.StepId, subscription.ExecutionPointerId, subscription.EventName, subscription.EventKey, long.Parse(subscription.Id), subscription.SubscriptionData);
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return subscription.Id;
    }

    public async Task TerminateEventSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _db.Deleteable<PersistedSubscription>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id.ToString() == subscriptionId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<EventSubscription?> GetEventSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var entity = await _db.Queryable<PersistedSubscription>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id.ToString() == subscriptionId)
            .FirstAsync(cancellationToken);

        if (entity == null)
            return null;

        return new EventSubscription
        {
            Id = entity.Id.ToString(),
            WorkflowId = entity.WorkflowId,
            StepId = entity.StepId,
            ExecutionPointerId = entity.ExecutionPointerId,
            EventName = entity.EventName,
            EventKey = entity.EventKey,
            SubscribeAsOf = entity.SubscribeAsOf.DateTime,
            SubscriptionData = entity.SubscriptionDataJson
        };
    }

    public async Task<IEnumerable<EventSubscription>> GetEventSubscriptionsAsync(string eventName, string eventKey, DateTime? asAt, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var query = _db.Queryable<PersistedSubscription>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.EventName == eventName && x.EventKey == eventKey);

        if (asAt.HasValue)
        {
            query = query.Where(x => x.SubscribeAsOf <= asAt.Value);
        }

        var entities = await query.ToListAsync(cancellationToken);

        return entities.Select(e => new EventSubscription
        {
            Id = e.Id.ToString(),
            WorkflowId = e.WorkflowId,
            StepId = e.StepId,
            EventName = e.EventName,
            EventKey = e.EventKey,
            SubscribeAsOf = e.SubscribeAsOf.DateTime,
            SubscriptionData = e.SubscriptionDataJson,
            ExecutionPointerId = e.ExecutionPointerId
        });
    }

    public async Task PersistErrorsAsync(IEnumerable<ExecutionError> errors, CancellationToken cancellationToken = default)
    {
        // 简化实现：可以将错误记录到日志或专门的错误表
        // 这里暂时不实现持久化，因为错误已经记录在 WorkflowInstance.ExecutionErrors 中
        await Task.CompletedTask;
    }

    // IScheduledCommandRepository 实现
    public bool SupportsScheduledCommands => false;

    public Task ScheduleCommand(ScheduledCommand command)
    {
        // 暂不支持计划命令
        throw new NotSupportedException("当前持久化提供者不支持计划命令");
    }

    public Task ScheduleCommandAsync(ScheduledCommand command, CancellationToken cancellationToken = default)
    {
        // 暂不支持计划命令
        throw new NotSupportedException("当前持久化提供者不支持计划命令");
    }

    public Task ProcessCommands(DateTimeOffset asOf, Func<ScheduledCommand, Task> action, CancellationToken cancellationToken = default)
    {
        // 暂不支持计划命令
        return Task.CompletedTask;
    }
}

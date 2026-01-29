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
        foreach (var pointer in workflow.ExecutionPointers)
        {
            var pointerEntity = CreatePointerEntity(tenantId, workflow.Id, pointer);
            await _db.Insertable(pointerEntity).ExecuteCommandAsync(cancellationToken);
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

    public async Task<WorkflowInstance?> GetWorkflowAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var entity = await _db.Queryable<PersistedWorkflow>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id.ToString() == workflowId)
            .FirstAsync(cancellationToken);

        if (entity == null)
            return null;

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

        // Load execution pointers
        var pointers = await _db.Queryable<PersistedExecutionPointer>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.WorkflowId == workflowId)
            .ToListAsync(cancellationToken);

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

    public async Task<IEnumerable<WorkflowInstance>> GetRunnableInstancesAsync(DateTime asAt, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var entities = await _db.Queryable<PersistedWorkflow>()
            .Where(x => x.TenantIdValue == tenantId.Value 
                && x.Status == WorkflowStatus.Runnable
                && (x.NextExecution == null || x.NextExecution <= asAt.Ticks))
            .ToListAsync(cancellationToken);

        var workflows = new List<WorkflowInstance>();
        foreach (var entity in entities)
        {
            var workflow = await GetWorkflowAsync(entity.Id.ToString(), cancellationToken);
            if (workflow != null)
            {
                workflows.Add(workflow);
            }
        }

        return workflows;
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
        foreach (var pointer in pointers)
        {
            var pointerEntity = await _db.Queryable<PersistedExecutionPointer>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.WorkflowId == workflow.Id && x.PointerId == pointer.Id)
                .FirstAsync(cancellationToken);

            if (pointerEntity != null)
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
                await _db.Updateable(pointerEntity).ExecuteCommandAsync(cancellationToken);
            }
            else
            {
                // Create new pointer if not exists
                var newPointerEntity = CreatePointerEntity(tenantId, workflow.Id, pointer);
                await _db.Insertable(newPointerEntity).ExecuteCommandAsync(cancellationToken);
            }
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

    public async Task<string> CreateEventSubscriptionAsync(EventSubscription subscription, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var entity = new PersistedSubscription(tenantId, subscription.WorkflowId, subscription.StepId, subscription.EventName, subscription.EventKey, long.Parse(subscription.Id), subscription.SubscriptionData);
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
            SubscriptionData = e.SubscriptionDataJson
        });
    }
}

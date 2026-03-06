using Atlas.Application.Approval.Abstractions;
using Atlas.Core.Events;
using Atlas.Infrastructure.Events.Approval;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.ApprovalFlow;

/// <summary>
/// Publishes approval domain events to all registered <see cref="IApprovalEventHandler"/> implementations,
/// and also bridges to the general-purpose <see cref="IEventBus"/> so other bounded contexts
/// can subscribe to approval events via <see cref="IDomainEventHandler{TEvent}"/>.
/// </summary>
public sealed class ApprovalEventPublisher
{
    private readonly IEnumerable<IApprovalEventHandler> _handlers;
    private readonly IEventBus _eventBus;
    private readonly ILogger<ApprovalEventPublisher>? _logger;

    public ApprovalEventPublisher(
        IEnumerable<IApprovalEventHandler> handlers,
        IEventBus eventBus,
        ILogger<ApprovalEventPublisher>? logger = null)
    {
        _handlers = handlers;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task PublishInstanceStartedAsync(ApprovalInstanceEvent e, CancellationToken ct)
    {
        foreach (var handler in _handlers)
        {
            try { await handler.OnInstanceStartedAsync(e, ct); }
            catch (Exception ex) { _logger?.LogError(ex, "Event handler failed: OnInstanceStarted, Instance={InstanceId}", e.InstanceId); }
        }

        try
        {
            await _eventBus.PublishAsync(new ApprovalInstanceDomainEvent
            {
                TenantId = e.TenantId,
                InstanceId = e.InstanceId,
                DefinitionId = e.DefinitionId,
                BusinessKey = e.BusinessKey,
                DataJson = e.DataJson,
                ActorUserId = e.ActorUserId,
                EventType = ApprovalInstanceEventType.Started
            }, ct);
        }
        catch (Exception ex) { _logger?.LogError(ex, "EventBus publish failed: ApprovalInstanceStarted, Instance={InstanceId}", e.InstanceId); }
    }

    public async Task PublishInstanceCompletedAsync(ApprovalInstanceEvent e, CancellationToken ct)
    {
        foreach (var handler in _handlers)
        {
            try { await handler.OnInstanceCompletedAsync(e, ct); }
            catch (Exception ex) { _logger?.LogError(ex, "Event handler failed: OnInstanceCompleted, Instance={InstanceId}", e.InstanceId); }
        }

        try
        {
            await _eventBus.PublishAsync(new ApprovalInstanceDomainEvent
            {
                TenantId = e.TenantId,
                InstanceId = e.InstanceId,
                DefinitionId = e.DefinitionId,
                BusinessKey = e.BusinessKey,
                DataJson = e.DataJson,
                ActorUserId = e.ActorUserId,
                EventType = ApprovalInstanceEventType.Completed
            }, ct);
        }
        catch (Exception ex) { _logger?.LogError(ex, "EventBus publish failed: ApprovalInstanceCompleted, Instance={InstanceId}", e.InstanceId); }
    }

    public async Task PublishInstanceRejectedAsync(ApprovalInstanceEvent e, CancellationToken ct)
    {
        foreach (var handler in _handlers)
        {
            try { await handler.OnInstanceRejectedAsync(e, ct); }
            catch (Exception ex) { _logger?.LogError(ex, "Event handler failed: OnInstanceRejected, Instance={InstanceId}", e.InstanceId); }
        }

        try
        {
            await _eventBus.PublishAsync(new ApprovalInstanceDomainEvent
            {
                TenantId = e.TenantId,
                InstanceId = e.InstanceId,
                DefinitionId = e.DefinitionId,
                BusinessKey = e.BusinessKey,
                DataJson = e.DataJson,
                ActorUserId = e.ActorUserId,
                EventType = ApprovalInstanceEventType.Rejected
            }, ct);
        }
        catch (Exception ex) { _logger?.LogError(ex, "EventBus publish failed: ApprovalInstanceRejected, Instance={InstanceId}", e.InstanceId); }
    }

    public async Task PublishInstanceCanceledAsync(ApprovalInstanceEvent e, CancellationToken ct)
    {
        foreach (var handler in _handlers)
        {
            try { await handler.OnInstanceCanceledAsync(e, ct); }
            catch (Exception ex) { _logger?.LogError(ex, "Event handler failed: OnInstanceCanceled, Instance={InstanceId}", e.InstanceId); }
        }

        try
        {
            await _eventBus.PublishAsync(new ApprovalInstanceDomainEvent
            {
                TenantId = e.TenantId,
                InstanceId = e.InstanceId,
                DefinitionId = e.DefinitionId,
                BusinessKey = e.BusinessKey,
                DataJson = e.DataJson,
                ActorUserId = e.ActorUserId,
                EventType = ApprovalInstanceEventType.Canceled
            }, ct);
        }
        catch (Exception ex) { _logger?.LogError(ex, "EventBus publish failed: ApprovalInstanceCanceled, Instance={InstanceId}", e.InstanceId); }
    }

    public async Task PublishTaskApprovedAsync(ApprovalTaskEvent e, CancellationToken ct)
    {
        foreach (var handler in _handlers)
        {
            try { await handler.OnTaskApprovedAsync(e, ct); }
            catch (Exception ex) { _logger?.LogError(ex, "Event handler failed: OnTaskApproved, Task={TaskId}", e.TaskId); }
        }

        try
        {
            await _eventBus.PublishAsync(new ApprovalTaskDomainEvent
            {
                TenantId = e.TenantId,
                InstanceId = e.InstanceId,
                TaskId = e.TaskId,
                NodeId = e.NodeId,
                BusinessKey = e.BusinessKey,
                ActorUserId = e.ActorUserId,
                Comment = e.Comment,
                EventType = ApprovalTaskEventType.Approved
            }, ct);
        }
        catch (Exception ex) { _logger?.LogError(ex, "EventBus publish failed: ApprovalTaskApproved, Task={TaskId}", e.TaskId); }
    }

    public async Task PublishTaskRejectedAsync(ApprovalTaskEvent e, CancellationToken ct)
    {
        foreach (var handler in _handlers)
        {
            try { await handler.OnTaskRejectedAsync(e, ct); }
            catch (Exception ex) { _logger?.LogError(ex, "Event handler failed: OnTaskRejected, Task={TaskId}", e.TaskId); }
        }

        try
        {
            await _eventBus.PublishAsync(new ApprovalTaskDomainEvent
            {
                TenantId = e.TenantId,
                InstanceId = e.InstanceId,
                TaskId = e.TaskId,
                NodeId = e.NodeId,
                BusinessKey = e.BusinessKey,
                ActorUserId = e.ActorUserId,
                Comment = e.Comment,
                EventType = ApprovalTaskEventType.Rejected
            }, ct);
        }
        catch (Exception ex) { _logger?.LogError(ex, "EventBus publish failed: ApprovalTaskRejected, Task={TaskId}", e.TaskId); }
    }
}

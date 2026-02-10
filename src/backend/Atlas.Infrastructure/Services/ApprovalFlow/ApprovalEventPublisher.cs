using Atlas.Application.Approval.Abstractions;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.ApprovalFlow;

/// <summary>
/// Publishes approval domain events to all registered <see cref="IApprovalEventHandler"/> implementations.
/// This decouples the approval module from external business modules (e.g., dynamic tables).
/// </summary>
public sealed class ApprovalEventPublisher
{
    private readonly IEnumerable<IApprovalEventHandler> _handlers;
    private readonly ILogger<ApprovalEventPublisher>? _logger;

    public ApprovalEventPublisher(
        IEnumerable<IApprovalEventHandler> handlers,
        ILogger<ApprovalEventPublisher>? logger = null)
    {
        _handlers = handlers;
        _logger = logger;
    }

    public async Task PublishInstanceStartedAsync(ApprovalInstanceEvent e, CancellationToken ct)
    {
        foreach (var handler in _handlers)
        {
            try { await handler.OnInstanceStartedAsync(e, ct); }
            catch (Exception ex) { _logger?.LogError(ex, "Event handler failed: OnInstanceStarted, Instance={InstanceId}", e.InstanceId); }
        }
    }

    public async Task PublishInstanceCompletedAsync(ApprovalInstanceEvent e, CancellationToken ct)
    {
        foreach (var handler in _handlers)
        {
            try { await handler.OnInstanceCompletedAsync(e, ct); }
            catch (Exception ex) { _logger?.LogError(ex, "Event handler failed: OnInstanceCompleted, Instance={InstanceId}", e.InstanceId); }
        }
    }

    public async Task PublishInstanceRejectedAsync(ApprovalInstanceEvent e, CancellationToken ct)
    {
        foreach (var handler in _handlers)
        {
            try { await handler.OnInstanceRejectedAsync(e, ct); }
            catch (Exception ex) { _logger?.LogError(ex, "Event handler failed: OnInstanceRejected, Instance={InstanceId}", e.InstanceId); }
        }
    }

    public async Task PublishInstanceCanceledAsync(ApprovalInstanceEvent e, CancellationToken ct)
    {
        foreach (var handler in _handlers)
        {
            try { await handler.OnInstanceCanceledAsync(e, ct); }
            catch (Exception ex) { _logger?.LogError(ex, "Event handler failed: OnInstanceCanceled, Instance={InstanceId}", e.InstanceId); }
        }
    }

    public async Task PublishTaskApprovedAsync(ApprovalTaskEvent e, CancellationToken ct)
    {
        foreach (var handler in _handlers)
        {
            try { await handler.OnTaskApprovedAsync(e, ct); }
            catch (Exception ex) { _logger?.LogError(ex, "Event handler failed: OnTaskApproved, Task={TaskId}", e.TaskId); }
        }
    }

    public async Task PublishTaskRejectedAsync(ApprovalTaskEvent e, CancellationToken ct)
    {
        foreach (var handler in _handlers)
        {
            try { await handler.OnTaskRejectedAsync(e, ct); }
            catch (Exception ex) { _logger?.LogError(ex, "Event handler failed: OnTaskRejected, Task={TaskId}", e.TaskId); }
        }
    }
}

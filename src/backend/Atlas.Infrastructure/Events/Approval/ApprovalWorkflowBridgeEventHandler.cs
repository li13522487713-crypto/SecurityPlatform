using Atlas.Core.Events;
using Atlas.Infrastructure.Events.Approval;
using Atlas.WorkflowCore.Abstractions;

namespace Atlas.Infrastructure.Events.Approval;

/// <summary>
/// 将审批领域事件桥接为 WorkflowCore 事件，驱动 WaitFor/ApprovalStep 继续执行。
/// </summary>
public sealed class ApprovalWorkflowBridgeEventHandler : IDomainEventHandler<ApprovalInstanceDomainEvent>
{
    private const string WorkflowApprovalEventName = "ApprovalDecision";
    private readonly IWorkflowHost _workflowHost;

    public ApprovalWorkflowBridgeEventHandler(IWorkflowHost workflowHost)
    {
        _workflowHost = workflowHost;
    }

    public async Task HandleAsync(ApprovalInstanceDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        if (domainEvent.EventType is not (ApprovalInstanceEventType.Completed or ApprovalInstanceEventType.Rejected))
        {
            return;
        }

        var payload = new
        {
            domainEvent.InstanceId,
            domainEvent.DefinitionId,
            domainEvent.BusinessKey,
            Status = domainEvent.EventType.ToString(),
            domainEvent.DataJson,
            domainEvent.ActorUserId,
            domainEvent.OccurredAt
        };

        if (!string.IsNullOrWhiteSpace(domainEvent.BusinessKey))
        {
            await _workflowHost.PublishEventAsync(
                WorkflowApprovalEventName,
                domainEvent.BusinessKey,
                payload,
                null,
                cancellationToken);
        }

        await _workflowHost.PublishEventAsync(
            WorkflowApprovalEventName,
            domainEvent.InstanceId.ToString(),
            payload,
            null,
            cancellationToken);
    }
}

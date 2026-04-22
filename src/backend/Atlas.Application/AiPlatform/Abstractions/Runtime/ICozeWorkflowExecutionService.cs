using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface ICozeWorkflowExecutionService
{
    Task<CozeWorkflowRunResult> SyncRunAsync(
        TenantId tenantId, long workflowId, long userId, CozeWorkflowRunCommand request, CancellationToken cancellationToken);

    Task CancelAsync(TenantId tenantId, long executionId, CancellationToken cancellationToken);

    Task ResumeAsync(TenantId tenantId, long executionId, string? data, CancellationToken cancellationToken);

    Task<CozeWorkflowRunResult> DebugNodeAsync(
        TenantId tenantId, long workflowId, long userId, CozeWorkflowNodeDebugCommand request, CancellationToken cancellationToken);
}

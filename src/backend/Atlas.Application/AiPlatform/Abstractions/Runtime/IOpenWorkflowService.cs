using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions.Runtime;

public interface IOpenWorkflowService
{
    Task<WorkflowV2DetailDto?> GetAsync(
        TenantId tenantId,
        long workflowId,
        CancellationToken cancellationToken);

    Task<WorkflowV2RunResult> RunAsync(
        TenantId tenantId,
        long workflowId,
        long userId,
        WorkflowV2RunRequest request,
        CancellationToken cancellationToken);

    IAsyncEnumerable<SseEvent> StreamAsync(
        TenantId tenantId,
        long workflowId,
        long userId,
        WorkflowV2RunRequest request,
        CancellationToken cancellationToken);

    Task ResumeAsync(
        TenantId tenantId,
        long executionId,
        WorkflowV2ResumeRequest request,
        CancellationToken cancellationToken);
}

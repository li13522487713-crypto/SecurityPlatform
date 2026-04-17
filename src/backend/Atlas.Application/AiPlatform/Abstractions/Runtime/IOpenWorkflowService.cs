using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions.Runtime;

public interface IOpenWorkflowService
{
    Task<DagWorkflowDetailDto?> GetAsync(
        TenantId tenantId,
        long workflowId,
        CancellationToken cancellationToken);

    Task<DagWorkflowRunResult> RunAsync(
        TenantId tenantId,
        long workflowId,
        long userId,
        DagWorkflowRunRequest request,
        CancellationToken cancellationToken);

    IAsyncEnumerable<SseEvent> StreamAsync(
        TenantId tenantId,
        long workflowId,
        long userId,
        DagWorkflowRunRequest request,
        CancellationToken cancellationToken);

    Task ResumeAsync(
        TenantId tenantId,
        long executionId,
        DagWorkflowResumeRequest request,
        CancellationToken cancellationToken);
}

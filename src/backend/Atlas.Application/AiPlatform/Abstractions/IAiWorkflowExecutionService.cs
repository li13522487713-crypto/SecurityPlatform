using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IAiWorkflowExecutionService
{
    Task<AiWorkflowExecutionRunResult> RunAsync(
        TenantId tenantId,
        long workflowDefinitionId,
        AiWorkflowExecutionRunRequest request,
        CancellationToken cancellationToken);

    Task CancelAsync(
        TenantId tenantId,
        string executionId,
        CancellationToken cancellationToken);

    Task<AiWorkflowExecutionProgressDto?> GetProgressAsync(
        TenantId tenantId,
        string executionId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AiWorkflowNodeHistoryItem>> GetNodeHistoryAsync(
        TenantId tenantId,
        string executionId,
        CancellationToken cancellationToken);
}

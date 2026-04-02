using Atlas.Application.LogicFlow.Flows.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LogicFlow.Flows.Abstractions;

public interface IFlowExecutionCommandService
{
    Task<long> TriggerAsync(
        FlowExecutionTriggerRequest request,
        TenantId tenantId,
        string userId,
        CancellationToken cancellationToken);

    Task CancelAsync(long executionId, TenantId tenantId, CancellationToken cancellationToken);

    Task<long> RetryAsync(long executionId, TenantId tenantId, string userId, CancellationToken cancellationToken);

    Task PauseAsync(long executionId, TenantId tenantId, CancellationToken cancellationToken);

    Task ResumeAsync(long executionId, TenantId tenantId, CancellationToken cancellationToken);
}

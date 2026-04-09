using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IOrchestrationExecutor
{
    Task<OrchestrationExecutionResult> ExecuteAsync(
        TenantId tenantId,
        OrchestrationExecutionRequest request,
        CancellationToken cancellationToken = default);
}

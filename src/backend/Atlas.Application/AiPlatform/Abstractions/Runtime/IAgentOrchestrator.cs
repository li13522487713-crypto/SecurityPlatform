using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IAgentOrchestrator
{
    Task<AgenticRagQueryResponse> OrchestrateAsync(
        TenantId tenantId,
        AgenticRagQueryRequest request,
        CancellationToken cancellationToken = default);
}

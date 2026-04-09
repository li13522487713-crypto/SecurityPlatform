using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IAgenticRagOrchestrationService
{
    Task<AgenticRagQueryResponse> QueryAsync(
        TenantId tenantId,
        AgenticRagQueryRequest request,
        CancellationToken cancellationToken = default);
}

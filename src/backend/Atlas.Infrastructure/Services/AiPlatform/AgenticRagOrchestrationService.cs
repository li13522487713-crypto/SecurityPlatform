using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AgenticRagOrchestrationService : IAgenticRagOrchestrationService
{
    private readonly IAgentOrchestrator _agentOrchestrator;

    public AgenticRagOrchestrationService(
        IAgentOrchestrator agentOrchestrator)
    {
        _agentOrchestrator = agentOrchestrator;
    }

    public async Task<AgenticRagQueryResponse> QueryAsync(
        TenantId tenantId,
        AgenticRagQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        return await _agentOrchestrator.OrchestrateAsync(tenantId, request, cancellationToken);
    }
}

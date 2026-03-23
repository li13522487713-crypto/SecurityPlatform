using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IAgentToolCallService
{
    Task<AgentToolCallResult> TryExecuteAsync(
        TenantId tenantId,
        long agentId,
        string userMessage,
        int maxIterations,
        CancellationToken cancellationToken);
}

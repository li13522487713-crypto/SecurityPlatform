using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions.Runtime;

public interface IAgentDebugService
{
    Task<AgentChatResponse> DebugAsync(
        TenantId tenantId,
        long userId,
        long agentId,
        AgentChatRequest request,
        CancellationToken cancellationToken);

    IAsyncEnumerable<AgentChatStreamEvent> StreamDebugAsync(
        TenantId tenantId,
        long userId,
        long agentId,
        AgentChatRequest request,
        CancellationToken cancellationToken);
}

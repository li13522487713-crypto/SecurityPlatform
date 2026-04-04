using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IAgentChatService
{
    Task<AgentChatResponse> ChatAsync(
        TenantId tenantId,
        long userId,
        long agentId,
        AgentChatRequest request,
        CancellationToken cancellationToken);

    IAsyncEnumerable<string> ChatStreamAsync(
        TenantId tenantId,
        long userId,
        long agentId,
        AgentChatRequest request,
        CancellationToken cancellationToken);

    IAsyncEnumerable<AgentChatStreamEvent> ChatEventStreamAsync(
        TenantId tenantId,
        long userId,
        long agentId,
        AgentChatRequest request,
        CancellationToken cancellationToken);

    Task CancelAsync(
        TenantId tenantId,
        long userId,
        long agentId,
        long conversationId,
        CancellationToken cancellationToken);
}

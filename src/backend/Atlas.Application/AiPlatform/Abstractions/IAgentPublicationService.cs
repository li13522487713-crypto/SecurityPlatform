using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IAgentPublicationService
{
    Task<IReadOnlyList<AgentPublicationListItem>> GetByAgentAsync(
        TenantId tenantId,
        long agentId,
        CancellationToken cancellationToken);

    Task<AgentPublicationPublishResult> PublishAsync(
        TenantId tenantId,
        long agentId,
        long publisherUserId,
        AgentPublicationPublishRequest request,
        CancellationToken cancellationToken);

    Task<AgentPublicationPublishResult> RollbackAsync(
        TenantId tenantId,
        long agentId,
        long publisherUserId,
        AgentPublicationRollbackRequest request,
        CancellationToken cancellationToken);

    Task<AgentEmbedTokenResult> RegenerateEmbedTokenAsync(
        TenantId tenantId,
        long agentId,
        CancellationToken cancellationToken);

    Task<AgentPublicationTokenContext> ResolveByEmbedTokenAsync(
        string embedToken,
        CancellationToken cancellationToken);
}

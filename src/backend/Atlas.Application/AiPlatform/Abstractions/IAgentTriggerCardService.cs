using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IAgentTriggerService
{
    Task<IReadOnlyList<AgentTriggerDto>> ListAsync(TenantId tenantId, long agentId, CancellationToken cancellationToken);
    Task<AgentTriggerDto> CreateAsync(TenantId tenantId, long agentId, long createdBy, AgentTriggerUpsertRequest request, CancellationToken cancellationToken);
    Task UpdateAsync(TenantId tenantId, long agentId, long triggerId, AgentTriggerUpsertRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(TenantId tenantId, long agentId, long triggerId, CancellationToken cancellationToken);
}

public interface IAgentCardService
{
    Task<IReadOnlyList<AgentCardDto>> ListAsync(TenantId tenantId, long agentId, CancellationToken cancellationToken);
    Task<AgentCardDto> CreateAsync(TenantId tenantId, long agentId, long createdBy, AgentCardUpsertRequest request, CancellationToken cancellationToken);
    Task UpdateAsync(TenantId tenantId, long agentId, long cardId, AgentCardUpsertRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(TenantId tenantId, long agentId, long cardId, CancellationToken cancellationToken);
}

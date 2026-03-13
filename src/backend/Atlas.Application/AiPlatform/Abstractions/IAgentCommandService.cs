using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IAgentCommandService
{
    Task<long> CreateAsync(
        TenantId tenantId,
        long creatorId,
        AgentCreateRequest request,
        CancellationToken cancellationToken);

    Task UpdateAsync(TenantId tenantId, long id, AgentUpdateRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken);

    Task<long> DuplicateAsync(TenantId tenantId, long creatorId, long id, CancellationToken cancellationToken);

    Task PublishAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
}

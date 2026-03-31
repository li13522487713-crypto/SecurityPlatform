using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface ITeamAgentPublicationService
{
    Task<IReadOnlyList<TeamAgentPublicationListItem>> GetByTeamAgentAsync(
        TenantId tenantId,
        long teamAgentId,
        CancellationToken cancellationToken);
}

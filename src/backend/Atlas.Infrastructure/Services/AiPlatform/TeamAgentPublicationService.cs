using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class TeamAgentPublicationService : ITeamAgentPublicationService
{
    private readonly TeamAgentPublicationRepository _publicationRepository;

    public TeamAgentPublicationService(TeamAgentPublicationRepository publicationRepository)
    {
        _publicationRepository = publicationRepository;
    }

    public async Task<IReadOnlyList<TeamAgentPublicationListItem>> GetByTeamAgentAsync(
        TenantId tenantId,
        long teamAgentId,
        CancellationToken cancellationToken)
    {
        var items = await _publicationRepository.GetByTeamAgentIdAsync(tenantId, teamAgentId, cancellationToken);
        return items.Select(item => new TeamAgentPublicationListItem(
            item.Id,
            item.TeamAgentId,
            item.Version,
            item.IsActive,
            item.ReleaseNote,
            item.PublishedByUserId,
            item.PublishedAt,
            item.RevokedAt.HasValue && item.RevokedAt.Value > DateTime.UnixEpoch
                ? item.RevokedAt
                : null)).ToList();
    }
}

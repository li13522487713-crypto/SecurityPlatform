using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Application.Identity.Repositories;

public interface IProjectPositionRepository
{
    Task<IReadOnlyList<ProjectPosition>> QueryByProjectIdAsync(
        TenantId tenantId,
        long projectId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<long>> QueryProjectIdsByPositionIdAsync(
        TenantId tenantId,
        long positionId,
        CancellationToken cancellationToken);
    Task DeleteByProjectIdAsync(TenantId tenantId, long projectId, CancellationToken cancellationToken);
    Task DeleteByPositionIdAsync(TenantId tenantId, long positionId, CancellationToken cancellationToken);
    Task AddRangeAsync(IReadOnlyList<ProjectPosition> entities, CancellationToken cancellationToken);
}

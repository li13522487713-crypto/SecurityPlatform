using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Application.Identity.Repositories;

public interface IUserPositionRepository
{
    Task<IReadOnlyList<UserPosition>> QueryByUserIdAsync(TenantId tenantId, long userId, CancellationToken cancellationToken);
    Task DeleteByUserIdAsync(TenantId tenantId, long userId, CancellationToken cancellationToken);
    Task DeleteByPositionIdAsync(TenantId tenantId, long positionId, CancellationToken cancellationToken);
    Task<bool> ExistsByPositionIdAsync(TenantId tenantId, long positionId, CancellationToken cancellationToken);
    Task AddRangeAsync(IReadOnlyList<UserPosition> userPositions, CancellationToken cancellationToken);
}

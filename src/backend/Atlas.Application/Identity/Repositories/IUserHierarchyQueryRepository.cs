using Atlas.Core.Tenancy;

namespace Atlas.Application.Identity.Repositories;

public interface IUserHierarchyQueryRepository
{
    Task<IReadOnlyList<long>> GetLeaderChainAsync(
        TenantId tenantId,
        long userId,
        int maxLevels,
        CancellationToken cancellationToken);

    Task<long?> GetLeaderAtLevelAsync(
        TenantId tenantId,
        long userId,
        int level,
        CancellationToken cancellationToken);
}

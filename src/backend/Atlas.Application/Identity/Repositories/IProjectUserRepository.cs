using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Application.Identity.Repositories;

public interface IProjectUserRepository
{
    Task<IReadOnlyList<ProjectUser>> QueryByProjectIdAsync(
        TenantId tenantId,
        long projectId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<long>> QueryUserIdsByProjectIdAsync(
        TenantId tenantId,
        long projectId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<long>> QueryProjectIdsByUserIdAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<long>> QueryUserIdsByProjectIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> projectIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ProjectUser>> QueryByUserIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> userIds,
        CancellationToken cancellationToken);
    Task<bool> ExistsAsync(TenantId tenantId, long projectId, long userId, CancellationToken cancellationToken);
    Task DeleteByProjectIdAsync(TenantId tenantId, long projectId, CancellationToken cancellationToken);
    Task DeleteByUserIdAsync(TenantId tenantId, long userId, CancellationToken cancellationToken);
    Task DeleteByUserAndProjectIdsAsync(
        TenantId tenantId,
        long userId,
        IReadOnlyList<long> projectIds,
        CancellationToken cancellationToken);
    Task AddRangeAsync(IReadOnlyList<ProjectUser> entities, CancellationToken cancellationToken);
}

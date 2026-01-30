using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Application.Identity.Repositories;

public interface IUserRoleRepository
{
    Task<IReadOnlyList<UserRole>> QueryByUserIdAsync(TenantId tenantId, long userId, CancellationToken cancellationToken);
    Task DeleteByUserIdAsync(TenantId tenantId, long userId, CancellationToken cancellationToken);
    Task DeleteByRoleIdAsync(TenantId tenantId, long roleId, CancellationToken cancellationToken);
    Task AddRangeAsync(IReadOnlyList<UserRole> userRoles, CancellationToken cancellationToken);
    Task<IReadOnlyList<long>> QueryRoleIdsByUserIdsAsync(TenantId tenantId, IReadOnlyList<long> userIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<long>> QueryUserIdsByRoleIdAsync(TenantId tenantId, long roleId, CancellationToken cancellationToken);
}

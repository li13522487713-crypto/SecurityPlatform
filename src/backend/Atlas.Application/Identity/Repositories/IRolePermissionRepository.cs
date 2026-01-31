using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Application.Identity.Repositories;

public interface IRolePermissionRepository
{
    Task<IReadOnlyList<RolePermission>> QueryByRoleIdAsync(TenantId tenantId, long roleId, CancellationToken cancellationToken);
    Task<IReadOnlyList<RolePermission>> QueryByRoleIdsAsync(TenantId tenantId, IReadOnlyList<long> roleIds, CancellationToken cancellationToken);
    Task DeleteByRoleIdAsync(TenantId tenantId, long roleId, CancellationToken cancellationToken);
    Task AddRangeAsync(IReadOnlyList<RolePermission> rolePermissions, CancellationToken cancellationToken);
}

using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Application.Identity.Repositories;

public interface IRoleMenuRepository
{
    Task<IReadOnlyList<RoleMenu>> QueryByRoleIdAsync(TenantId tenantId, long roleId, CancellationToken cancellationToken);
    Task DeleteByRoleIdAsync(TenantId tenantId, long roleId, CancellationToken cancellationToken);
    Task DeleteByMenuIdAsync(TenantId tenantId, long menuId, CancellationToken cancellationToken);
    Task AddRangeAsync(IReadOnlyList<RoleMenu> roleMenus, CancellationToken cancellationToken);
}

using Atlas.Core.Enums;
using Atlas.Core.Tenancy;
using Atlas.Application.Identity.Models;

namespace Atlas.Application.Identity.Abstractions;

public interface IRoleCommandService
{
    Task<long> CreateAsync(TenantId tenantId, RoleCreateRequest request, long id, CancellationToken cancellationToken);
    Task UpdateAsync(TenantId tenantId, long roleId, RoleUpdateRequest request, CancellationToken cancellationToken);

    Task UpdatePermissionsAsync(
        TenantId tenantId,
        long roleId,
        IReadOnlyList<long> permissionIds,
        CancellationToken cancellationToken);

    Task UpdateMenusAsync(
        TenantId tenantId,
        long roleId,
        IReadOnlyList<long> menuIds,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        long roleId,
        CancellationToken cancellationToken);

    /// <summary>更新角色数据权限范围（等保2.0）</summary>
    Task SetDataScopeAsync(TenantId tenantId, long roleId, DataScopeType scope, IReadOnlyList<long>? deptIds, CancellationToken cancellationToken);
}

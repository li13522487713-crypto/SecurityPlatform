using Atlas.Application.Identity.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class RolePermissionRepository : IRolePermissionRepository
{
    private readonly ISqlSugarClient _db;

    public RolePermissionRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<RolePermission>> QueryByRoleIdAsync(
        TenantId tenantId,
        long roleId,
        CancellationToken cancellationToken)
    {
        var list = await _db.Queryable<RolePermission>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.RoleId == roleId)
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task<IReadOnlyList<RolePermission>> QueryByRoleIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> roleIds,
        CancellationToken cancellationToken)
    {
        if (roleIds.Count == 0)
        {
            return Array.Empty<RolePermission>();
        }

        var distinctIds = roleIds.Distinct().ToArray();
        return await _db.Queryable<RolePermission>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(distinctIds, x.RoleId))
            .ToListAsync(cancellationToken);
    }

    public Task DeleteByRoleIdAsync(TenantId tenantId, long roleId, CancellationToken cancellationToken)
    {
        return _db.Deleteable<RolePermission>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.RoleId == roleId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task AddRangeAsync(IReadOnlyList<RolePermission> rolePermissions, CancellationToken cancellationToken)
    {
        if (rolePermissions.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _db.Insertable(rolePermissions.ToList()).ExecuteCommandAsync(cancellationToken);
    }
}

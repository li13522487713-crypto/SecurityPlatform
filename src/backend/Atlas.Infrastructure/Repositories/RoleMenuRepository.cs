using Atlas.Application.Identity.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class RoleMenuRepository : IRoleMenuRepository
{
    private readonly ISqlSugarClient _db;

    public RoleMenuRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<RoleMenu>> QueryByRoleIdAsync(
        TenantId tenantId,
        long roleId,
        CancellationToken cancellationToken)
    {
        var list = await _db.Queryable<RoleMenu>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.RoleId == roleId)
            .ToListAsync(cancellationToken);
        return list;
    }

    public Task DeleteByRoleIdAsync(TenantId tenantId, long roleId, CancellationToken cancellationToken)
    {
        return _db.Deleteable<RoleMenu>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.RoleId == roleId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteByMenuIdAsync(TenantId tenantId, long menuId, CancellationToken cancellationToken)
    {
        return _db.Deleteable<RoleMenu>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.MenuId == menuId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task AddRangeAsync(IReadOnlyList<RoleMenu> roleMenus, CancellationToken cancellationToken)
    {
        if (roleMenus.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _db.Insertable(roleMenus.ToList()).ExecuteCommandAsync(cancellationToken);
    }
}

using Atlas.Application.Identity.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class MenuRepository : RepositoryBase<Menu>, IMenuRepository
{
    public MenuRepository(ISqlSugarClient db) : base(db) { }

    public async Task<(IReadOnlyList<Menu> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        string? keyword,
        bool? isHidden,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<Menu>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword) || x.Path.Contains(keyword));
        }
        if (isHidden.HasValue)
        {
            query = query.Where(x => x.IsHidden == isHidden.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var list = await query
            .OrderBy(x => x.SortOrder, OrderByType.Asc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (list, totalCount);
    }

    public async Task<IReadOnlyList<Menu>> QueryAllAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var list = await Db.Queryable<Menu>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .OrderBy(x => x.SortOrder, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task<bool> HasChildrenAsync(TenantId tenantId, long menuId, CancellationToken cancellationToken)
    {
        return await Db.Queryable<Menu>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ParentId == menuId)
            .CountAsync(cancellationToken) > 0;
    }

    public async Task<bool> ExistsByPathAsync(TenantId tenantId, string path, long? excludeMenuId, CancellationToken cancellationToken)
    {
        // 仅对非按钮类型的菜单（M/C/L）检查路径唯一性；按钮（F）通常无独立路径
        var query = Db.Queryable<Menu>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Path == path && x.MenuType != "F");
        if (excludeMenuId.HasValue)
        {
            query = query.Where(x => x.Id != excludeMenuId.Value);
        }
        return await query.CountAsync(cancellationToken) > 0;
    }

    public async Task BatchUpdateSortOrderAsync(
        TenantId tenantId,
        IReadOnlyList<(long MenuId, int SortOrder)> updates,
        CancellationToken cancellationToken)
    {
        if (updates.Count == 0)
        {
            return;
        }

        // 批量更新 SortOrder，避免在循环中逐条执行数据库操作
        var menuIds = updates.Select(u => u.MenuId).ToArray();
        var existing = await Db.Queryable<Menu>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(menuIds, x.Id))
            .ToListAsync(cancellationToken);

        var sortMap = updates.ToDictionary(u => u.MenuId, u => u.SortOrder);
        foreach (var menu in existing)
        {
            if (sortMap.TryGetValue(menu.Id, out var newSort))
            {
                menu.UpdateSortOrder(newSort);
            }
        }

        if (existing.Count > 0)
        {
            await Db.Updateable(existing)
                .UpdateColumns(x => new { x.SortOrder })
                .ExecuteCommandAsync(cancellationToken);
        }
    }
}

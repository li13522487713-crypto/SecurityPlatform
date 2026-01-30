using Atlas.Application.Identity.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class MenuRepository : IMenuRepository
{
    private readonly ISqlSugarClient _db;

    public MenuRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<Menu?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return await _db.Queryable<Menu>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Menu> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<Menu>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword) || x.Path.Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var list = await query
            .OrderBy(x => x.SortOrder, OrderByType.Asc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (list, totalCount);
    }

    public async Task<IReadOnlyList<Menu>> QueryAllAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var list = await _db.Queryable<Menu>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .OrderBy(x => x.SortOrder, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task<IReadOnlyList<Menu>> QueryByIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<Menu>();
        }

        return await _db.Queryable<Menu>()
            .Where(x => x.TenantIdValue == tenantId.Value && ids.Contains(x.Id))
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(Menu menu, CancellationToken cancellationToken)
    {
        return _db.Insertable(menu).ExecuteCommandAsync(cancellationToken);
    }

    public Task UpdateAsync(Menu menu, CancellationToken cancellationToken)
    {
        return _db.Updateable(menu).ExecuteCommandAsync(cancellationToken);
    }
}

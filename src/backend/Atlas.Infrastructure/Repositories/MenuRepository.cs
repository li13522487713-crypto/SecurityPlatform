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
}

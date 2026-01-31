using Atlas.Application.TableViews.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class TableViewRepository : ITableViewRepository
{
    private readonly ISqlSugarClient _db;

    public TableViewRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<TableView?> FindByIdAsync(
        TenantId tenantId,
        long userId,
        long id,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<TableView>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<TableView?> FindByNameAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        string name,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<TableView>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.UserId == userId
                && x.TableKey == tableKey
                && x.Name == name)
            .FirstAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<TableView> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        int pageIndex,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<TableView>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId && x.TableKey == tableKey);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var list = await query
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (list, totalCount);
    }

    public Task AddAsync(TableView view, CancellationToken cancellationToken)
    {
        return _db.Insertable(view).ExecuteCommandAsync(cancellationToken);
    }

    public Task UpdateAsync(TableView view, CancellationToken cancellationToken)
    {
        return _db.Updateable(view)
            .Where(x => x.Id == view.Id && x.TenantIdValue == view.TenantIdValue && x.UserId == view.UserId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteAsync(TenantId tenantId, long userId, long id, CancellationToken cancellationToken)
    {
        return _db.Deleteable<TableView>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId && x.Id == id)
            .ExecuteCommandAsync(cancellationToken);
    }
}

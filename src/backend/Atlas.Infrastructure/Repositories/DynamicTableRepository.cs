using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class DynamicTableRepository : IDynamicTableRepository
{
    private readonly ISqlSugarClient _db;

    public DynamicTableRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<DynamicTable?> FindByKeyAsync(
        TenantId tenantId,
        string tableKey,
        long? appId,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<DynamicTable>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TableKey == tableKey);
        query = ApplyAppScope(query, appId);

        return await query.FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DynamicTable>> QueryByKeysAsync(
        TenantId tenantId,
        IReadOnlyList<string> tableKeys,
        long? appId,
        CancellationToken cancellationToken)
    {
        if (tableKeys.Count == 0)
        {
            return Array.Empty<DynamicTable>();
        }

        var normalized = tableKeys
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalized.Length == 0)
        {
            return Array.Empty<DynamicTable>();
        }

        var query = _db.Queryable<DynamicTable>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(normalized, x.TableKey));
        query = ApplyAppScope(query, appId);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<DynamicTable> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        string? keyword,
        long? appId,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<DynamicTable>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        query = ApplyAppScope(query, appId);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.TableKey.Contains(keyword) || x.DisplayName.Contains(keyword));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (items, total);
    }

    public Task AddAsync(DynamicTable table, CancellationToken cancellationToken)
    {
        return _db.Insertable(table).ExecuteCommandAsync(cancellationToken);
    }

    public Task UpdateAsync(DynamicTable table, CancellationToken cancellationToken)
    {
        return _db.Updateable(table)
            .Where(x => x.Id == table.Id && x.TenantIdValue == table.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return _db.Deleteable<DynamicTable>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .ExecuteCommandAsync(cancellationToken);
    }

    private static ISugarQueryable<DynamicTable> ApplyAppScope(
        ISugarQueryable<DynamicTable> query,
        long? appId)
    {
        return appId.HasValue
            ? query.Where(x => x.AppId == appId.Value)
            : query.Where(x => x.AppId == null);
    }
}

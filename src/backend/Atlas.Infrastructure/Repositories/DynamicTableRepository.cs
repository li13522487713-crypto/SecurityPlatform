using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Infrastructure.Services;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class DynamicTableRepository : IDynamicTableRepository
{
    private readonly ISqlSugarClient _mainDb;
    private readonly IAppDbScopeFactory _appDbScopeFactory;
    private readonly IAppContextAccessor _appContextAccessor;

    public DynamicTableRepository(
        ISqlSugarClient mainDb,
        IAppDbScopeFactory appDbScopeFactory,
        IAppContextAccessor appContextAccessor)
    {
        _mainDb = mainDb;
        _appDbScopeFactory = appDbScopeFactory;
        _appContextAccessor = appContextAccessor;
    }

    public DynamicTableRepository(ISqlSugarClient db)
        : this(db, new MainOnlyAppDbScopeFactory(db), NullAppContextAccessor.Instance)
    {
    }

    public async Task<DynamicTable?> FindByKeyAsync(
        TenantId tenantId,
        string tableKey,
        long? appId,
        CancellationToken cancellationToken)
    {
        var db = await GetDbAsync(tenantId, appId, cancellationToken);
        var query = db.Queryable<DynamicTable>()
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

        var db = await GetDbAsync(tenantId, appId, cancellationToken);
        var query = db.Queryable<DynamicTable>()
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
        var db = await GetDbAsync(tenantId, appId, cancellationToken);
        var query = db.Queryable<DynamicTable>()
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
        return ExecuteInDbAsync(
            new TenantId(table.TenantIdValue),
            table.AppId,
            db => db.Insertable(table).ExecuteCommandAsync(cancellationToken),
            cancellationToken);
    }

    public Task UpdateAsync(DynamicTable table, CancellationToken cancellationToken)
    {
        return ExecuteInDbAsync(
            new TenantId(table.TenantIdValue),
            table.AppId,
            db => db.Updateable(table)
                .Where(x => x.Id == table.Id && x.TenantIdValue == table.TenantIdValue)
                .ExecuteCommandAsync(cancellationToken),
            cancellationToken);
    }

    public Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return ExecuteInDbAsync(
            tenantId,
            _appContextAccessor.ResolveAppId(),
            db => db.Deleteable<DynamicTable>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
                .ExecuteCommandAsync(cancellationToken),
            cancellationToken);
    }

    private async Task<ISqlSugarClient> GetDbAsync(TenantId tenantId, long? appId, CancellationToken cancellationToken)
    {
        if (appId.HasValue && appId.Value > 0)
        {
            return await _appDbScopeFactory.GetAppClientAsync(tenantId, appId.Value, cancellationToken);
        }

        return _mainDb;
    }

    private async Task ExecuteInDbAsync(
        TenantId tenantId,
        long? appId,
        Func<ISqlSugarClient, Task> operation,
        CancellationToken cancellationToken)
    {
        var db = await GetDbAsync(tenantId, appId, cancellationToken);
        await operation(db);
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

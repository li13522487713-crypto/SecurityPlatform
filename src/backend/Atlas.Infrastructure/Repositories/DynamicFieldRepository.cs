using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Infrastructure.Services;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class DynamicFieldRepository : IDynamicFieldRepository
{
    private readonly ISqlSugarClient _mainDb;
    private readonly IAppDbScopeFactory _appDbScopeFactory;
    private readonly IAppContextAccessor _appContextAccessor;

    public DynamicFieldRepository(
        ISqlSugarClient mainDb,
        IAppDbScopeFactory appDbScopeFactory,
        IAppContextAccessor appContextAccessor)
    {
        _mainDb = mainDb;
        _appDbScopeFactory = appDbScopeFactory;
        _appContextAccessor = appContextAccessor;
    }

    public DynamicFieldRepository(ISqlSugarClient db)
        : this(db, new MainOnlyAppDbScopeFactory(db), NullAppContextAccessor.Instance)
    {
    }

    public async Task<IReadOnlyList<DynamicField>> ListByTableIdAsync(
        TenantId tenantId,
        long tableId,
        CancellationToken cancellationToken)
    {
        var db = await GetDbAsync(tenantId, cancellationToken);
        var list = await db.Queryable<DynamicField>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TableId == tableId)
            .OrderBy(x => x.SortOrder, OrderByType.Asc)
            .ToListAsync(cancellationToken);

        return list;
    }

    public async Task<IReadOnlyList<DynamicField>> ListByTableIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> tableIds,
        CancellationToken cancellationToken)
    {
        if (tableIds.Count == 0)
        {
            return Array.Empty<DynamicField>();
        }

        var ids = tableIds.Distinct().ToArray();
        var db = await GetDbAsync(tenantId, cancellationToken);
        return await db.Queryable<DynamicField>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(ids, x.TableId))
            .ToListAsync(cancellationToken);
    }

    public Task AddRangeAsync(IReadOnlyList<DynamicField> fields, CancellationToken cancellationToken)
    {
        if (fields.Count == 0)
        {
            return Task.CompletedTask;
        }

        return ExecuteInDbAsync(
            new TenantId(fields[0].TenantIdValue),
            db => db.Insertable(fields.ToList()).ExecuteCommandAsync(cancellationToken),
            cancellationToken);
    }

    public async Task UpdateRangeAsync(IReadOnlyList<DynamicField> fields, CancellationToken cancellationToken)
    {
        if (fields.Count == 0)
        {
            return;
        }

        var db = await GetDbAsync(new TenantId(fields[0].TenantIdValue), cancellationToken);
        var affected = await db.Updateable(fields.ToList())
            .WhereColumns(x => new { x.Id, x.TenantIdValue })
            .ExecuteCommandAsync(cancellationToken);

        if (affected == 0 && fields.Count > 0)
        {
            throw new InvalidOperationException("批量更新动态字段失败。");
        }
    }

    public Task DeleteByTableIdAsync(TenantId tenantId, long tableId, CancellationToken cancellationToken)
    {
        return ExecuteInDbAsync(
            tenantId,
            db => db.Deleteable<DynamicField>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.TableId == tableId)
                .ExecuteCommandAsync(cancellationToken),
            cancellationToken);
    }

    private async Task<ISqlSugarClient> GetDbAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var appId = _appContextAccessor.ResolveAppId();
        if (appId.HasValue && appId.Value > 0)
        {
            return await _appDbScopeFactory.GetAppClientAsync(tenantId, appId.Value, cancellationToken);
        }

        return _mainDb;
    }

    private async Task ExecuteInDbAsync(
        TenantId tenantId,
        Func<ISqlSugarClient, Task> operation,
        CancellationToken cancellationToken)
    {
        var db = await GetDbAsync(tenantId, cancellationToken);
        await operation(db);
    }
}

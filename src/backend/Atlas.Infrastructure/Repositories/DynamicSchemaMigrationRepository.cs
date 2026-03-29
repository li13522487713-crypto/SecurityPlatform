using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Infrastructure.Services;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class DynamicSchemaMigrationRepository : IDynamicSchemaMigrationRepository
{
    private readonly ISqlSugarClient _mainDb;
    private readonly IAppDbScopeFactory _appDbScopeFactory;
    private readonly IAppContextAccessor _appContextAccessor;

    public DynamicSchemaMigrationRepository(
        ISqlSugarClient mainDb,
        IAppDbScopeFactory appDbScopeFactory,
        IAppContextAccessor appContextAccessor)
    {
        _mainDb = mainDb;
        _appDbScopeFactory = appDbScopeFactory;
        _appContextAccessor = appContextAccessor;
    }

    public DynamicSchemaMigrationRepository(ISqlSugarClient db)
        : this(db, new MainOnlyAppDbScopeFactory(db), NullAppContextAccessor.Instance)
    {
    }

    public async Task<(IReadOnlyList<DynamicSchemaMigration> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        long tableId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var db = await GetDbAsync(tenantId, cancellationToken);
        var query = db.Queryable<DynamicSchemaMigration>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TableId == tableId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (items, total);
    }

    public Task AddAsync(DynamicSchemaMigration migration, CancellationToken cancellationToken)
    {
        return ExecuteInDbAsync(
            new TenantId(migration.TenantIdValue),
            db => db.Insertable(migration).ExecuteCommandAsync(cancellationToken),
            cancellationToken);
    }

    public async Task<DynamicSchemaMigration?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        var db = await GetDbAsync(tenantId, cancellationToken);
        return await db.Queryable<DynamicSchemaMigration>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
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

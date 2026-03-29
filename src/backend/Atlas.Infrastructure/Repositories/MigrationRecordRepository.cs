using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Infrastructure.Services;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class MigrationRecordRepository : IMigrationRecordRepository
{
    private readonly ISqlSugarClient _mainDb;
    private readonly IAppDbScopeFactory _appDbScopeFactory;
    private readonly IAppContextAccessor _appContextAccessor;

    public MigrationRecordRepository(
        ISqlSugarClient mainDb,
        IAppDbScopeFactory appDbScopeFactory,
        IAppContextAccessor appContextAccessor)
    {
        _mainDb = mainDb;
        _appDbScopeFactory = appDbScopeFactory;
        _appContextAccessor = appContextAccessor;
    }

    public MigrationRecordRepository(ISqlSugarClient db)
        : this(db, new MainOnlyAppDbScopeFactory(db), NullAppContextAccessor.Instance)
    {
    }

    public async Task<(IReadOnlyList<MigrationRecord> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        string? keyword,
        string? tableKey,
        CancellationToken cancellationToken)
    {
        var db = await GetDbAsync(tenantId, cancellationToken);
        var query = db.Queryable<MigrationRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.TableKey.Contains(keyword) || x.Status.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(tableKey))
        {
            query = query.Where(x => x.TableKey == tableKey);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (items, totalCount);
    }

    public async Task<MigrationRecord?> FindByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        var db = await GetDbAsync(tenantId, cancellationToken);
        return await db.Queryable<MigrationRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<MigrationRecord?> FindByVersionAsync(
        TenantId tenantId,
        string tableKey,
        int version,
        CancellationToken cancellationToken)
    {
        var db = await GetDbAsync(tenantId, cancellationToken);
        return await db.Queryable<MigrationRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TableKey == tableKey && x.Version == version)
            .FirstAsync(cancellationToken);
    }

    public Task AddAsync(MigrationRecord entity, CancellationToken cancellationToken)
    {
        return ExecuteInDbAsync(
            new TenantId(entity.TenantIdValue),
            db => db.Insertable(entity).ExecuteCommandAsync(cancellationToken),
            cancellationToken);
    }

    public Task UpdateAsync(MigrationRecord entity, CancellationToken cancellationToken)
    {
        return ExecuteInDbAsync(
            new TenantId(entity.TenantIdValue),
            db => db.Updateable(entity)
                .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
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

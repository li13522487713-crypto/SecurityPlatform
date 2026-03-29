using Atlas.Application.Platform.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using Atlas.Domain.Platform.Entities;
using Atlas.Infrastructure.Services;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class RuntimeRouteRepository : IRuntimeRouteRepository
{
    private readonly ISqlSugarClient _mainDb;
    private readonly IAppDbScopeFactory _appDbScopeFactory;

    public RuntimeRouteRepository(ISqlSugarClient db, IAppDbScopeFactory appDbScopeFactory)
    {
        _mainDb = db;
        _appDbScopeFactory = appDbScopeFactory;
    }
    public RuntimeRouteRepository(ISqlSugarClient db) : this(db, new MainOnlyAppDbScopeFactory(db)) { }

    public async Task<RuntimeRoute?> GetByAppAndPageKeyAsync(
        TenantId tenantId,
        string appKey,
        string pageKey,
        CancellationToken cancellationToken = default)
    {
        var db = await ResolveDbByAppKeyAsync(tenantId, appKey, cancellationToken);
        return await db.Queryable<RuntimeRoute>()
            .FirstAsync(x =>
                x.TenantIdValue == tenantId.Value &&
                x.AppKey == appKey &&
                x.PageKey == pageKey,
                cancellationToken);
    }

    public async Task UpsertAsync(RuntimeRoute route, CancellationToken cancellationToken = default)
    {
        var tenantId = new TenantId(route.TenantIdValue);
        var db = await ResolveDbByAppKeyAsync(tenantId, route.AppKey, cancellationToken);
        var existing = await db.Queryable<RuntimeRoute>()
            .FirstAsync(x =>
                x.TenantIdValue == route.TenantIdValue &&
                x.AppKey == route.AppKey &&
                x.PageKey == route.PageKey,
                cancellationToken);

        if (existing is null)
        {
            await db.Insertable(route).ExecuteCommandAsync(cancellationToken);
            return;
        }

        existing.RebindManifest(route.ManifestId);
        if (route.IsActive)
        {
            existing.Activate(route.SchemaVersion, route.EnvironmentCode);
        }
        else
        {
            existing.Disable();
        }

        await db.Updateable(existing).ExecuteCommandAsync(cancellationToken);
    }

    private async Task<ISqlSugarClient> ResolveDbByAppKeyAsync(TenantId tenantId, string appKey, CancellationToken cancellationToken)
    {
        var app = await _mainDb.Queryable<LowCodeApp>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppKey == appKey)
            .FirstAsync(cancellationToken);
        if (app is not null && app.Id > 0)
        {
            return await _appDbScopeFactory.GetAppClientAsync(tenantId, app.Id, cancellationToken);
        }

        return _mainDb;
    }
}

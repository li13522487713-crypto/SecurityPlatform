using Atlas.Application.Platform.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Platform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class RuntimeRouteRepository : IRuntimeRouteRepository
{
    private readonly ISqlSugarClient _db;

    public RuntimeRouteRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<RuntimeRoute?> GetByAppAndPageKeyAsync(
        TenantId tenantId,
        string appKey,
        string pageKey,
        CancellationToken cancellationToken = default)
    {
        return await _db.Queryable<RuntimeRoute>()
            .FirstAsync(x =>
                x.TenantIdValue == tenantId.Value &&
                x.AppKey == appKey &&
                x.PageKey == pageKey,
                cancellationToken);
    }

    public async Task UpsertAsync(RuntimeRoute route, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Queryable<RuntimeRoute>()
            .FirstAsync(x =>
                x.TenantIdValue == route.TenantIdValue &&
                x.AppKey == route.AppKey &&
                x.PageKey == route.PageKey,
                cancellationToken);

        if (existing is null)
        {
            await _db.Insertable(route).ExecuteCommandAsync(cancellationToken);
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

        await _db.Updateable(existing).ExecuteCommandAsync(cancellationToken);
    }
}

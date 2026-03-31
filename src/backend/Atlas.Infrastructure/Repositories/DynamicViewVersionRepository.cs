using Atlas.Application.DynamicViews.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicViews.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class DynamicViewVersionRepository : IDynamicViewVersionRepository
{
    private readonly ISqlSugarClient _db;

    public DynamicViewVersionRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<int> GetLatestVersionAsync(TenantId tenantId, long? appId, string viewKey, CancellationToken cancellationToken)
    {
        var query = BuildQuery(tenantId, appId, viewKey);
        var latest = await query.MaxAsync(x => (int?)x.Version, cancellationToken);
        return latest ?? 0;
    }

    public Task AddAsync(DynamicViewVersion entity, CancellationToken cancellationToken)
    {
        return _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DynamicViewVersion>> ListByViewKeyAsync(TenantId tenantId, long? appId, string viewKey, CancellationToken cancellationToken)
    {
        var list = await BuildQuery(tenantId, appId, viewKey)
            .OrderBy(x => x.Version, OrderByType.Desc)
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task<DynamicViewVersion?> FindByVersionAsync(TenantId tenantId, long? appId, string viewKey, int version, CancellationToken cancellationToken)
    {
        return await BuildQuery(tenantId, appId, viewKey)
            .Where(x => x.Version == version)
            .FirstAsync(cancellationToken);
    }

    private ISugarQueryable<DynamicViewVersion> BuildQuery(TenantId tenantId, long? appId, string viewKey)
    {
        var query = _db.Queryable<DynamicViewVersion>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ViewKey == viewKey);
        return appId.HasValue
            ? query.Where(x => x.AppId == appId.Value)
            : query.Where(x => x.AppId == null);
    }
}

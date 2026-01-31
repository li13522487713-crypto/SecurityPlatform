using Atlas.Application.Identity.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AppConfigRepository : IAppConfigRepository
{
    private readonly ISqlSugarClient _db;

    public AppConfigRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<AppConfig?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return await _db.Queryable<AppConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<AppConfig?> FindByAppIdAsync(TenantId tenantId, string appId, CancellationToken cancellationToken)
    {
        return await _db.Queryable<AppConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .FirstAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<AppConfig> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<AppConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.AppId.Contains(keyword) || x.Name.Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var list = await query
            .OrderBy(x => x.SortOrder, OrderByType.Asc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (list, totalCount);
    }

    public Task AddAsync(AppConfig appConfig, CancellationToken cancellationToken)
    {
        return _db.Insertable(appConfig).ExecuteCommandAsync(cancellationToken);
    }

    public Task UpdateAsync(AppConfig appConfig, CancellationToken cancellationToken)
    {
        return _db.Updateable(appConfig)
            .Where(x => x.Id == appConfig.Id && x.TenantIdValue == appConfig.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }
}

using Atlas.Application.LowCode.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class LowCodeAppVersionRepository : ILowCodeAppVersionRepository
{
    private readonly ISqlSugarClient _db;

    public LowCodeAppVersionRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<LowCodeAppVersion?> GetByIdAsync(
        TenantId tenantId,
        long appId,
        long id,
        CancellationToken cancellationToken = default)
    {
        return await _db.Queryable<LowCodeAppVersion>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<LowCodeAppVersion> Items, int Total)> GetPagedAsync(
        TenantId tenantId,
        long appId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Queryable<LowCodeAppVersion>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.Version, OrderByType.Desc)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (items, total);
    }

    public async Task<int> GetLatestVersionAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken = default)
    {
        var latest = await _db.Queryable<LowCodeAppVersion>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .OrderBy(x => x.Version, OrderByType.Desc)
            .Select(x => x.Version)
            .FirstAsync(cancellationToken);

        return latest <= 0 ? 0 : latest;
    }

    public Task InsertAsync(LowCodeAppVersion entity, CancellationToken cancellationToken = default)
    {
        return _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }
}

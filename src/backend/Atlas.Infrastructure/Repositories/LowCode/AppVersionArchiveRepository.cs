using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories.LowCode;

public sealed class AppVersionArchiveRepository : IAppVersionArchiveRepository
{
    private readonly ISqlSugarClient _db;

    public AppVersionArchiveRepository(ISqlSugarClient db) => _db = db;

    public async Task<long> InsertAsync(AppVersionArchive archive, CancellationToken cancellationToken)
    {
        await _db.Insertable(archive).ExecuteCommandAsync(cancellationToken);
        return archive.Id;
    }

    public Task<AppVersionArchive?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return _db.Queryable<AppVersionArchive>()
            .Where(x => x.Id == id && x.TenantIdValue == tenantId.Value)
            .FirstAsync(cancellationToken)!;
    }

    public async Task<IReadOnlyList<AppVersionArchive>> ListByAppAsync(TenantId tenantId, long appId, bool includeSystemSnapshot, CancellationToken cancellationToken)
    {
        var q = _db.Queryable<AppVersionArchive>()
            .Where(x => x.AppId == appId && x.TenantIdValue == tenantId.Value);
        if (!includeSystemSnapshot)
        {
            q = q.Where(x => x.IsSystemSnapshot == false);
        }
        var list = await q.OrderBy(x => x.CreatedAt, OrderByType.Desc).ToListAsync(cancellationToken);
        return list;
    }
}

using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories.LowCode;

public sealed class AppFaqRepository : IAppFaqRepository
{
    private readonly ISqlSugarClient _db;
    public AppFaqRepository(ISqlSugarClient db) => _db = db;

    public async Task<long> InsertAsync(AppFaqEntry entry, CancellationToken cancellationToken)
    {
        await _db.Insertable(entry).ExecuteCommandAsync(cancellationToken);
        return entry.Id;
    }
    public async Task<bool> UpdateAsync(AppFaqEntry entry, CancellationToken cancellationToken)
    {
        var rows = await _db.Updateable(entry).Where(x => x.Id == entry.Id && x.TenantIdValue == entry.TenantIdValue).ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }
    public async Task<bool> DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var rows = await _db.Deleteable<AppFaqEntry>().Where(x => x.TenantIdValue == tenantId.Value && x.Id == id).ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }
    public Task<AppFaqEntry?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
        => _db.Queryable<AppFaqEntry>().Where(x => x.TenantIdValue == tenantId.Value && x.Id == id).FirstAsync(cancellationToken)!;

    public async Task<IReadOnlyList<AppFaqEntry>> SearchAsync(TenantId tenantId, string? keyword, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var q = _db.Queryable<AppFaqEntry>().Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            q = q.Where(x => x.Title.Contains(keyword) || x.Body.Contains(keyword) || (x.Tags != null && x.Tags.Contains(keyword)));
        }
        return await q.OrderBy(x => x.Hits, OrderByType.Desc).OrderBy(x => x.UpdatedAt, OrderByType.Desc).ToPageListAsync(pageIndex, pageSize, cancellationToken);
    }
}

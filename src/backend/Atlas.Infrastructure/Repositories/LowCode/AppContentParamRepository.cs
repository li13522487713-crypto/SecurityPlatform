using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories.LowCode;

public sealed class AppContentParamRepository : IAppContentParamRepository
{
    private readonly ISqlSugarClient _db;

    public AppContentParamRepository(ISqlSugarClient db) => _db = db;

    public async Task<long> InsertAsync(AppContentParam param, CancellationToken cancellationToken)
    {
        await _db.Insertable(param).ExecuteCommandAsync(cancellationToken);
        return param.Id;
    }

    public async Task<bool> UpdateAsync(AppContentParam param, CancellationToken cancellationToken)
    {
        var rows = await _db.Updateable(param)
            .Where(x => x.Id == param.Id && x.AppId == param.AppId && x.TenantIdValue == param.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken)
    {
        var rows = await _db.Deleteable<AppContentParam>()
            .Where(x => x.Id == id && x.AppId == appId && x.TenantIdValue == tenantId.Value)
            .ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }

    public Task<AppContentParam?> FindByIdAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken)
    {
        return _db.Queryable<AppContentParam>()
            .Where(x => x.Id == id && x.AppId == appId && x.TenantIdValue == tenantId.Value)
            .FirstAsync(cancellationToken)!;
    }

    public async Task<bool> ExistsCodeAsync(TenantId tenantId, long appId, string code, long? excludeId, CancellationToken cancellationToken)
    {
        var q = _db.Queryable<AppContentParam>()
            .Where(x => x.AppId == appId && x.TenantIdValue == tenantId.Value && x.Code == code);
        if (excludeId.HasValue)
        {
            q = q.Where(x => x.Id != excludeId.Value);
        }
        return await q.AnyAsync();
    }

    public async Task<IReadOnlyList<AppContentParam>> ListByAppAsync(TenantId tenantId, long appId, CancellationToken cancellationToken)
    {
        var list = await _db.Queryable<AppContentParam>()
            .Where(x => x.AppId == appId && x.TenantIdValue == tenantId.Value)
            .OrderBy(x => x.Code, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        return list;
    }
}

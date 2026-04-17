using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories.LowCode;

public sealed class AppVariableRepository : IAppVariableRepository
{
    private readonly ISqlSugarClient _db;

    public AppVariableRepository(ISqlSugarClient db) => _db = db;

    public async Task<long> InsertAsync(AppVariable variable, CancellationToken cancellationToken)
    {
        await _db.Insertable(variable).ExecuteCommandAsync(cancellationToken);
        return variable.Id;
    }

    public async Task<bool> UpdateAsync(AppVariable variable, CancellationToken cancellationToken)
    {
        var rows = await _db.Updateable(variable)
            .Where(x => x.Id == variable.Id && x.AppId == variable.AppId && x.TenantIdValue == variable.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken)
    {
        var rows = await _db.Deleteable<AppVariable>()
            .Where(x => x.Id == id && x.AppId == appId && x.TenantIdValue == tenantId.Value)
            .ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }

    public Task<AppVariable?> FindByIdAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken)
    {
        return _db.Queryable<AppVariable>()
            .Where(x => x.Id == id && x.AppId == appId && x.TenantIdValue == tenantId.Value)
            .FirstAsync(cancellationToken)!;
    }

    public async Task<bool> ExistsCodeAsync(TenantId tenantId, long appId, string code, long? excludeId, CancellationToken cancellationToken)
    {
        var q = _db.Queryable<AppVariable>()
            .Where(x => x.AppId == appId && x.TenantIdValue == tenantId.Value && x.Code == code);
        if (excludeId.HasValue)
        {
            q = q.Where(x => x.Id != excludeId.Value);
        }
        return await q.AnyAsync();
    }

    public async Task<IReadOnlyList<AppVariable>> ListByAppAsync(TenantId tenantId, long appId, string? scope, CancellationToken cancellationToken)
    {
        var q = _db.Queryable<AppVariable>()
            .Where(x => x.AppId == appId && x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(scope))
        {
            q = q.Where(x => x.Scope == scope);
        }
        var list = await q.OrderBy(x => x.Code, OrderByType.Asc).ToListAsync(cancellationToken);
        return list;
    }
}

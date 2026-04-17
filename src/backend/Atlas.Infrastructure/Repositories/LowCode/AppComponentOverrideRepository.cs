using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories.LowCode;

public sealed class AppComponentOverrideRepository : IAppComponentOverrideRepository
{
    private readonly ISqlSugarClient _db;

    public AppComponentOverrideRepository(ISqlSugarClient db) => _db = db;

    public async Task<IReadOnlyList<AppComponentOverride>> ListAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var list = await _db.Queryable<AppComponentOverride>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .ToListAsync(cancellationToken);
        return list;
    }

    public Task<AppComponentOverride?> FindAsync(TenantId tenantId, string type, CancellationToken cancellationToken)
    {
        return _db.Queryable<AppComponentOverride>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Type == type)
            .FirstAsync(cancellationToken)!;
    }

    public async Task<long> InsertAsync(AppComponentOverride entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(AppComponentOverride entity, CancellationToken cancellationToken)
    {
        var rows = await _db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(TenantId tenantId, string type, CancellationToken cancellationToken)
    {
        var rows = await _db.Deleteable<AppComponentOverride>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Type == type)
            .ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }
}

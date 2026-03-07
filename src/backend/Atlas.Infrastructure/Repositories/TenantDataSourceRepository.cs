using Atlas.Domain.System.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class TenantDataSourceRepository
{
    private readonly ISqlSugarClient _db;

    public TenantDataSourceRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<TenantDataSource?> FindByTenantIdAsync(string tenantId, CancellationToken ct = default)
    {
        return await _db.Queryable<TenantDataSource>()
            .Where(x => x.TenantIdValue == tenantId && x.IsActive)
            .FirstAsync(ct);
    }

    public async Task<List<TenantDataSource>> QueryAllAsync(CancellationToken ct = default)
    {
        return await _db.Queryable<TenantDataSource>()
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .ToListAsync(ct);
    }

    public async Task<TenantDataSource?> FindByIdAsync(long id, CancellationToken ct = default)
    {
        return await _db.Queryable<TenantDataSource>()
            .Where(x => x.Id == id)
            .FirstAsync(ct);
    }

    public async Task AddAsync(TenantDataSource entity, CancellationToken ct = default)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(ct);
    }

    public async Task UpdateAsync(TenantDataSource entity, CancellationToken ct = default)
    {
        await _db.Updateable(entity).ExecuteCommandAsync(ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        await _db.Deleteable<TenantDataSource>().Where(x => x.Id == id).ExecuteCommandAsync(ct);
    }
}

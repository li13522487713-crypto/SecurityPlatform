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

    public async Task<List<TenantDataSource>> QueryByTenantAsync(string tenantId, CancellationToken ct = default)
    {
        return await _db.Queryable<TenantDataSource>()
            .Where(x => x.TenantIdValue == tenantId)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .ToListAsync(ct);
    }

    public async Task<TenantDataSource?> FindByIdAsync(long id, CancellationToken ct = default)
    {
        return await _db.Queryable<TenantDataSource>()
            .Where(x => x.Id == id)
            .FirstAsync(ct);
    }

    public async Task<TenantDataSource?> FindByTenantAndIdAsync(string tenantId, long id, CancellationToken ct = default)
    {
        return await _db.Queryable<TenantDataSource>()
            .Where(x => x.TenantIdValue == tenantId && x.Id == id)
            .FirstAsync(ct);
    }

    public async Task<TenantDataSource?> FindByTenantAndAppIdAsync(string tenantId, long appId, CancellationToken ct = default)
    {
        return await _db.Queryable<TenantDataSource>()
            .Where(x => x.TenantIdValue == tenantId && x.AppId == appId)
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .FirstAsync(ct);
    }

    public async Task<TenantDataSource?> FindByTenantAndAppInstanceBindingAsync(
        Guid tenantId,
        long appInstanceId,
        CancellationToken ct = default)
    {
        var binding = await _db.Queryable<TenantAppDataSourceBinding>()
            .Where(x => x.TenantIdValue == tenantId && x.TenantAppInstanceId == appInstanceId && x.IsActive)
            .OrderBy(x => x.BindingType, OrderByType.Asc)
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .FirstAsync(ct);
        if (binding is null)
        {
            return null;
        }

        return await _db.Queryable<TenantDataSource>()
            .Where(x => x.Id == binding.DataSourceId
                && x.TenantIdValue == tenantId.ToString()
                && x.IsActive)
            .FirstAsync(ct);
    }

    public async Task AddAsync(TenantDataSource entity, CancellationToken ct = default)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(ct);
    }

    public async Task UpdateAsync(TenantDataSource entity, CancellationToken ct = default)
    {
        await _db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(ct);
    }

    public async Task<bool> DeleteByTenantAndIdAsync(string tenantId, long id, CancellationToken ct = default)
    {
        var affected = await _db.Deleteable<TenantDataSource>()
            .Where(x => x.TenantIdValue == tenantId && x.Id == id)
            .ExecuteCommandAsync(ct);
        return affected > 0;
    }
}

using Atlas.Application.LowCode.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class LowCodeAppRepository : ILowCodeAppRepository
{
    private readonly ISqlSugarClient _db;

    public LowCodeAppRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<LowCodeApp?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default)
    {
        return await _db.Queryable<LowCodeApp>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<LowCodeApp?> GetByKeyAsync(TenantId tenantId, string appKey, CancellationToken cancellationToken = default)
    {
        return await _db.Queryable<LowCodeApp>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppKey == appKey)
            .FirstAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<LowCodeApp> Items, int Total)> GetPagedAsync(
        TenantId tenantId, int pageIndex, int pageSize, string? keyword, string? category,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Queryable<LowCodeApp>()
            .Where(x => x.TenantIdValue == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword) || x.AppKey.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(x => x.Category == category);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (items, total);
    }

    public Task InsertAsync(LowCodeApp entity, CancellationToken cancellationToken = default)
    {
        return _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public Task UpdateAsync(LowCodeApp entity, CancellationToken cancellationToken = default)
    {
        return _db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        return _db.Deleteable<LowCodeApp>()
            .Where(x => x.Id == id)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<bool> ExistsByKeyAsync(TenantId tenantId, string appKey, long? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _db.Queryable<LowCodeApp>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppKey == appKey);

        if (excludeId.HasValue)
        {
            query = query.Where(x => x.Id != excludeId.Value);
        }

        return await query.AnyAsync();
    }
}

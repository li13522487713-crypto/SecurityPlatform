using Atlas.Application.LowCode.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class LowCodePageRepository : ILowCodePageRepository
{
    private readonly ISqlSugarClient _db;

    public LowCodePageRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<LowCodePage?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default)
    {
        return await _db.Queryable<LowCodePage>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<LowCodePage?> GetByKeyAsync(TenantId tenantId, long appId, string pageKey, CancellationToken cancellationToken = default)
    {
        return await _db.Queryable<LowCodePage>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.PageKey == pageKey)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LowCodePage>> GetByAppIdAsync(TenantId tenantId, long appId, CancellationToken cancellationToken = default)
    {
        return await _db.Queryable<LowCodePage>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LowCodePage>> GetPublishedPagesAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        return await _db.Queryable<LowCodePage>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.IsPublished)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);
    }

    public Task InsertAsync(LowCodePage entity, CancellationToken cancellationToken = default)
    {
        return _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public Task AddRangeAsync(IReadOnlyList<LowCodePage> entities, CancellationToken cancellationToken = default)
    {
        if (entities.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _db.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public Task UpdateAsync(LowCodePage entity, CancellationToken cancellationToken = default)
    {
        return _db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        return _db.Deleteable<LowCodePage>()
            .Where(x => x.Id == id)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteByAppIdAsync(long appId, CancellationToken cancellationToken = default)
    {
        return _db.Deleteable<LowCodePage>()
            .Where(x => x.AppId == appId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<bool> ExistsByKeyAsync(TenantId tenantId, long appId, string pageKey, long? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _db.Queryable<LowCodePage>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.PageKey == pageKey);

        if (excludeId.HasValue)
        {
            query = query.Where(x => x.Id != excludeId.Value);
        }

        return await query.AnyAsync();
    }
}

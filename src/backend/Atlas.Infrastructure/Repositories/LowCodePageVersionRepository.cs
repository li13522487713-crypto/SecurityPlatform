using Atlas.Application.LowCode.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class LowCodePageVersionRepository : ILowCodePageVersionRepository
{
    private readonly ISqlSugarClient _db;

    public LowCodePageVersionRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<LowCodePageVersion?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken = default)
    {
        return await _db.Queryable<LowCodePageVersion>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LowCodePageVersion>> GetByPageIdAsync(
        TenantId tenantId,
        long pageId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Queryable<LowCodePageVersion>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.PageId == pageId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LowCodePageVersion>> GetByAppIdAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Queryable<LowCodePageVersion>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task InsertAsync(LowCodePageVersion entity, CancellationToken cancellationToken = default)
    {
        return _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public Task AddRangeAsync(IReadOnlyList<LowCodePageVersion> entities, CancellationToken cancellationToken = default)
    {
        if (entities.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _db.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteByPageIdAsync(TenantId tenantId, long pageId, CancellationToken cancellationToken = default)
    {
        return _db.Deleteable<LowCodePageVersion>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.PageId == pageId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteByAppIdAsync(TenantId tenantId, long appId, CancellationToken cancellationToken = default)
    {
        return _db.Deleteable<LowCodePageVersion>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .ExecuteCommandAsync(cancellationToken);
    }
}

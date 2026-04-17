using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class PlatformContentRepository : RepositoryBase<PlatformContent>
{
    public PlatformContentRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<PlatformContent>> ListBySlotAsync(
        TenantId tenantId,
        string slot,
        bool onlyActive,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<PlatformContent>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Slot == slot);

        if (onlyActive)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderBy(x => x.OrderIndex, OrderByType.Asc)
            .OrderBy(x => x.PublishedAt, OrderByType.Desc)
            .ToListAsync(cancellationToken);
    }

    public async Task<PlatformContent?> FindAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<PlatformContent>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    /// <summary>
    /// 运营 UI 用：按可选 slot 过滤 + onlyActive 控制下架内容可见性。
    /// </summary>
    public async Task<IReadOnlyList<PlatformContent>> ListAsync(
        TenantId tenantId,
        string? slot,
        bool onlyActive,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<PlatformContent>()
            .Where(x => x.TenantIdValue == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(slot))
        {
            query = query.Where(x => x.Slot == slot);
        }

        if (onlyActive)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderBy(x => x.Slot, OrderByType.Asc)
            .OrderBy(x => x.OrderIndex, OrderByType.Asc)
            .OrderBy(x => x.PublishedAt, OrderByType.Desc)
            .ToListAsync(cancellationToken);
    }
}

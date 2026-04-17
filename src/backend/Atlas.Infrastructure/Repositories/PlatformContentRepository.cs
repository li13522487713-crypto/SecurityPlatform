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
}

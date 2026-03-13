using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AiRecentEditRepository : RepositoryBase<AiRecentEdit>
{
    public AiRecentEditRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<List<AiRecentEdit>> GetRecentByUserAsync(
        TenantId tenantId,
        long userId,
        int limit,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<AiRecentEdit>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId)
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<AiRecentEdit?> FindByResourceAsync(
        TenantId tenantId,
        long userId,
        string resourceType,
        long resourceId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<AiRecentEdit>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && x.UserId == userId
                && x.ResourceType == resourceType
                && x.ResourceId == resourceId)
            .FirstAsync(cancellationToken);
    }
}

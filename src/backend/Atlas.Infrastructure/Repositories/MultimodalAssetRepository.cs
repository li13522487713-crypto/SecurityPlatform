using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class MultimodalAssetRepository : RepositoryBase<MultimodalAsset>
{
    public MultimodalAssetRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<MultimodalAsset>> GetLatestByUserAsync(
        TenantId tenantId,
        long userId,
        int take,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<MultimodalAsset>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.CreatedByUserId == userId)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}

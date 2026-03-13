using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AiMarketplaceFavoriteRepository : RepositoryBase<AiMarketplaceFavorite>
{
    public AiMarketplaceFavoriteRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<bool> ExistsAsync(
        TenantId tenantId,
        long userId,
        long productId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<AiMarketplaceFavorite>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId && x.ProductId == productId)
            .CountAsync(cancellationToken) > 0;
    }

    public async Task<List<long>> GetProductIdsByUserAsync(
        TenantId tenantId,
        long userId,
        IReadOnlyCollection<long> productIds,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return [];
        }

        var productIdArray = productIds.ToArray();
        return await Db.Queryable<AiMarketplaceFavorite>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && x.UserId == userId
                && SqlFunc.ContainsArray(productIdArray, x.ProductId))
            .Select(x => x.ProductId)
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteByUserAndProductAsync(
        TenantId tenantId,
        long userId,
        long productId,
        CancellationToken cancellationToken)
    {
        await Db.Deleteable<AiMarketplaceFavorite>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId && x.ProductId == productId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task DeleteByProductIdAsync(TenantId tenantId, long productId, CancellationToken cancellationToken)
    {
        await Db.Deleteable<AiMarketplaceFavorite>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ProductId == productId)
            .ExecuteCommandAsync(cancellationToken);
    }
}

using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AiProductCategoryRepository : RepositoryBase<AiProductCategory>
{
    public AiProductCategoryRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<List<AiProductCategory>> GetEnabledAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        return await Db.Queryable<AiProductCategory>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.IsEnabled)
            .OrderBy(x => x.SortOrder, OrderByType.Asc)
            .OrderBy(x => x.CreatedAt, OrderByType.Asc)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<AiProductCategory>> GetByIdsAsync(
        TenantId tenantId,
        IReadOnlyCollection<long> categoryIds,
        CancellationToken cancellationToken)
    {
        if (categoryIds.Count == 0)
        {
            return [];
        }

        var categoryIdArray = categoryIds.ToArray();
        return await Db.Queryable<AiProductCategory>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(categoryIdArray, x.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByCodeAsync(
        TenantId tenantId,
        string code,
        long? excludeId,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<AiProductCategory>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Code == code);
        if (excludeId.HasValue && excludeId.Value > 0)
        {
            query = query.Where(x => x.Id != excludeId.Value);
        }

        return await query.CountAsync(cancellationToken) > 0;
    }

    public async Task<bool> IsCategoryUsedByProductsAsync(TenantId tenantId, long categoryId, CancellationToken cancellationToken)
    {
        return await Db.Queryable<AiMarketplaceProduct>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.CategoryId == categoryId)
            .CountAsync(cancellationToken) > 0;
    }
}

using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AiMarketplaceProductRepository : RepositoryBase<AiMarketplaceProduct>
{
    public AiMarketplaceProductRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<(List<AiMarketplaceProduct> Items, long Total)> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        long? categoryId,
        AiMarketplaceProductType? productType,
        AiMarketplaceProductStatus? status,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<AiMarketplaceProduct>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalized = keyword.Trim();
            query = query.Where(x =>
                x.Name.Contains(normalized)
                || x.Summary!.Contains(normalized)
                || x.Description!.Contains(normalized));
        }

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(x => x.CategoryId == categoryId.Value);
        }

        if (productType.HasValue)
        {
            query = query.Where(x => x.ProductType == productType.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.PublishedAt, OrderByType.Desc)
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (items, total);
    }

    public async Task<bool> ExistsByNameAsync(
        TenantId tenantId,
        string name,
        long? excludeId,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<AiMarketplaceProduct>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Name == name);
        if (excludeId.HasValue && excludeId.Value > 0)
        {
            query = query.Where(x => x.Id != excludeId.Value);
        }

        return await query.CountAsync(cancellationToken) > 0;
    }
}

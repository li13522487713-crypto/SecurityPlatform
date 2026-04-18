using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class KnowledgeBaseRepository : RepositoryBase<KnowledgeBase>
{
    public KnowledgeBaseRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<(List<KnowledgeBase> Items, long Total)> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<KnowledgeBase>()
            .Where(x => x.TenantIdValue == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalized = keyword.Trim();
            query = query.Where(x => x.Name.Contains(normalized) || x.Description!.Contains(normalized));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (items, total);
    }

    public async Task<int> CountByTenantAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        return await Db.Queryable<KnowledgeBase>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .CountAsync(cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(
        TenantId tenantId,
        string name,
        long? excludeId,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<KnowledgeBase>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Name == name);

        if (excludeId.HasValue && excludeId.Value > 0)
        {
            query = query.Where(x => x.Id != excludeId.Value);
        }

        return await query.CountAsync(cancellationToken) > 0;
    }
}

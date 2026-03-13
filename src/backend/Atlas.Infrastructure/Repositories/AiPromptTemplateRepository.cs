using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AiPromptTemplateRepository : RepositoryBase<AiPromptTemplate>
{
    public AiPromptTemplateRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<(List<AiPromptTemplate> Items, long Total)> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        string? category,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<AiPromptTemplate>()
            .Where(x => x.TenantIdValue == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalized = keyword.Trim();
            query = query.Where(x => x.Name.Contains(normalized) || x.Description!.Contains(normalized));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var normalizedCategory = category.Trim();
            query = query.Where(x => x.Category == normalizedCategory);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
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
        var query = Db.Queryable<AiPromptTemplate>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Name == name);
        if (excludeId.HasValue && excludeId.Value > 0)
        {
            query = query.Where(x => x.Id != excludeId.Value);
        }

        return await query.CountAsync(cancellationToken) > 0;
    }
}

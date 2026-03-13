using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Infrastructure.Repositories;

public sealed class ModelConfigRepository : RepositoryBase<ModelConfig>
{
    public ModelConfigRepository(SqlSugar.ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<(List<ModelConfig> Items, long Total)> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<ModelConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                x.Name.Contains(keyword) ||
                x.ProviderType.Contains(keyword) ||
                x.DefaultModel.Contains(keyword));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.Id, SqlSugar.OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (items, total);
    }

    public async Task<List<ModelConfig>> GetAllEnabledAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        return await Db.Queryable<ModelConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.IsEnabled)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<ModelConfig?> FindByNameAsync(TenantId tenantId, string name, CancellationToken cancellationToken)
    {
        return await Db.Queryable<ModelConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Name == name)
            .FirstAsync(cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(TenantId tenantId, string name, CancellationToken cancellationToken)
    {
        var count = await Db.Queryable<ModelConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Name == name)
            .CountAsync(cancellationToken);
        return count > 0;
    }
}

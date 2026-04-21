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
        string? workspaceId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = BuildFilteredQuery(tenantId, keyword, workspaceId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.Id, SqlSugar.OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (items, total);
    }

    public async Task<(long Total, long Enabled, long EmbeddingCount)> GetStatsAsync(
        TenantId tenantId,
        string? keyword,
        string? workspaceId,
        CancellationToken cancellationToken)
    {
        var total = await BuildFilteredQuery(tenantId, keyword, workspaceId).CountAsync(cancellationToken);
        var enabled = await BuildFilteredQuery(tenantId, keyword, workspaceId)
            .Where(x => x.IsEnabled)
            .CountAsync(cancellationToken);
        var embeddingCount = await BuildFilteredQuery(tenantId, keyword, workspaceId)
            .Where(x => x.SupportsEmbedding)
            .CountAsync(cancellationToken);

        return (total, enabled, embeddingCount);
    }

    public async Task<List<ModelConfig>> GetAllEnabledAsync(
        TenantId tenantId,
        string? workspaceId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<ModelConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.IsEnabled)
            .WhereIF(!string.IsNullOrWhiteSpace(workspaceId), x => x.WorkspaceId == workspaceId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<ModelConfig?> FindByNameAsync(
        TenantId tenantId,
        string name,
        string? workspaceId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<ModelConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Name == name)
            .WhereIF(!string.IsNullOrWhiteSpace(workspaceId), x => x.WorkspaceId == workspaceId)
            .FirstAsync(cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(
        TenantId tenantId,
        string name,
        string? workspaceId,
        CancellationToken cancellationToken)
    {
        var count = await Db.Queryable<ModelConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Name == name)
            .WhereIF(!string.IsNullOrWhiteSpace(workspaceId), x => x.WorkspaceId == workspaceId)
            .CountAsync(cancellationToken);
        return count > 0;
    }

    private SqlSugar.ISugarQueryable<ModelConfig> BuildFilteredQuery(
        TenantId tenantId,
        string? keyword,
        string? workspaceId)
    {
        var query = Db.Queryable<ModelConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(workspaceId))
        {
            query = query.Where(x => x.WorkspaceId == workspaceId);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                x.Name.Contains(keyword) ||
                x.ProviderType.Contains(keyword) ||
                x.DefaultModel.Contains(keyword));
        }

        return query;
    }
}

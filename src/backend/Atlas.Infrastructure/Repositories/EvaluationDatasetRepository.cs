using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class EvaluationDatasetRepository : RepositoryBase<EvaluationDataset>
{
    public EvaluationDatasetRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<(List<EvaluationDataset> Items, long Total)> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<EvaluationDataset>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalized = keyword.Trim();
            query = query.Where(x =>
                x.Name.Contains(normalized) ||
                x.Description.Contains(normalized) ||
                x.Scene.Contains(normalized));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (items, total);
    }

    /// <summary>
    /// Coze 测试集（PRD 05-4.8）专用：按 Scene 前缀（如 "coze-testset:&lt;workspaceId&gt;"）过滤，
    /// 等价于"按工作空间维度查测试集"。Scene 字段在 Coze 场景下被复用为索引列。
    /// </summary>
    public async Task<(List<EvaluationDataset> Items, long Total)> GetPagedByScenePrefixAsync(
        TenantId tenantId,
        string scenePrefix,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<EvaluationDataset>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Scene.StartsWith(scenePrefix));

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalized = keyword.Trim();
            query = query.Where(x =>
                x.Name.Contains(normalized) ||
                x.Description.Contains(normalized));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (items, total);
    }

    public async Task<List<EvaluationDataset>> GetByScenePrefixAsync(
        TenantId tenantId,
        string scenePrefix,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<EvaluationDataset>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Scene.StartsWith(scenePrefix))
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<EvaluationDataset>> GetBySceneTokenAsync(
        TenantId tenantId,
        string sceneToken,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<EvaluationDataset>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Scene == sceneToken)
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToListAsync(cancellationToken);
    }
}

using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class EvaluationCaseRepository : RepositoryBase<EvaluationCase>
{
    public EvaluationCaseRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<EvaluationCase>> GetByDatasetAsync(
        TenantId tenantId,
        long datasetId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<EvaluationCase>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DatasetId == datasetId)
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<long, int>> CountByDatasetIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> datasetIds,
        CancellationToken cancellationToken)
    {
        if (datasetIds.Count == 0)
        {
            return [];
        }

        var idArray = datasetIds.Distinct().ToArray();
        var rows = await Db.Queryable<EvaluationCase>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(idArray, x.DatasetId))
            .GroupBy(x => x.DatasetId)
            .Select(x => new { DatasetId = x.DatasetId, Count = SqlFunc.AggregateCount(x.Id) })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(x => x.DatasetId, x => x.Count);
    }
}

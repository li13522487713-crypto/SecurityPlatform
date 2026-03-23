using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class EvaluationResultRepository : RepositoryBase<EvaluationResult>
{
    public EvaluationResultRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task AddRangeAsync(
        IReadOnlyList<EvaluationResult> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        await Db.Insertable(items.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EvaluationResult>> GetByTaskAsync(
        TenantId tenantId,
        long taskId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<EvaluationResult>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TaskId == taskId)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToListAsync(cancellationToken);
    }
}

using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class EvaluationTaskRepository : RepositoryBase<EvaluationTask>
{
    public EvaluationTaskRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<(List<EvaluationTask> Items, long Total)> GetPagedAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<EvaluationTask>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (items, total);
    }

    public async Task<EvaluationTask?> FindByIdAnyTenantAsync(long id)
    {
        var entity = await Db.Queryable<EvaluationTask>()
            .Where(x => x.Id == id)
            .FirstAsync();
        return entity;
    }
}

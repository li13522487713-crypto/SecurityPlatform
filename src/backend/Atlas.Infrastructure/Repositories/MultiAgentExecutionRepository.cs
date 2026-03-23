using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class MultiAgentExecutionRepository : RepositoryBase<MultiAgentExecution>
{
    public MultiAgentExecutionRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<MultiAgentExecution?> FindLatestByOrchestrationAsync(
        TenantId tenantId,
        long orchestrationId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<MultiAgentExecution>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.OrchestrationId == orchestrationId)
            .OrderBy(x => x.StartedAt, OrderByType.Desc)
            .OrderBy(x => x.Id, OrderByType.Desc)
            .FirstAsync(cancellationToken);
    }
}

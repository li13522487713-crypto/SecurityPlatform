using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AiWorkflowSnapshotRepository : RepositoryBase<AiWorkflowSnapshot>
{
    public AiWorkflowSnapshotRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<AiWorkflowSnapshot>> GetByWorkflowIdAsync(
        TenantId tenantId,
        long workflowDefinitionId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<AiWorkflowSnapshot>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.WorkflowDefinitionId == workflowDefinitionId)
            .OrderBy(x => x.Version, OrderByType.Desc)
            .ToListAsync(cancellationToken);
    }

    public async Task<AiWorkflowSnapshot?> GetByVersionAsync(
        TenantId tenantId,
        long workflowDefinitionId,
        int version,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<AiWorkflowSnapshot>()
            .Where(x => x.TenantIdValue == tenantId.Value
                        && x.WorkflowDefinitionId == workflowDefinitionId
                        && x.Version == version)
            .FirstAsync(cancellationToken);
    }
}

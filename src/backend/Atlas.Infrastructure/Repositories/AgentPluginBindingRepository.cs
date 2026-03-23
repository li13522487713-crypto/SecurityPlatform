using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AgentPluginBindingRepository : RepositoryBase<AgentPluginBinding>
{
    public AgentPluginBindingRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<List<AgentPluginBinding>> GetByAgentIdAsync(
        TenantId tenantId,
        long agentId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<AgentPluginBinding>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AgentId == agentId)
            .OrderBy(x => x.SortOrder, OrderByType.Asc)
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteByAgentIdAsync(TenantId tenantId, long agentId, CancellationToken cancellationToken)
    {
        await Db.Deleteable<AgentPluginBinding>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AgentId == agentId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IReadOnlyCollection<AgentPluginBinding> entities, CancellationToken cancellationToken)
    {
        if (entities.Count == 0)
        {
            return;
        }

        await Db.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }
}

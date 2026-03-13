using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AgentKnowledgeLinkRepository : RepositoryBase<AgentKnowledgeLink>
{
    public AgentKnowledgeLinkRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<List<AgentKnowledgeLink>> GetByAgentIdAsync(
        TenantId tenantId,
        long agentId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<AgentKnowledgeLink>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AgentId == agentId)
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteByAgentIdAsync(TenantId tenantId, long agentId, CancellationToken cancellationToken)
    {
        await Db.Deleteable<AgentKnowledgeLink>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AgentId == agentId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IReadOnlyCollection<AgentKnowledgeLink> entities, CancellationToken cancellationToken)
    {
        if (entities.Count == 0)
        {
            return;
        }

        await Db.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }
}

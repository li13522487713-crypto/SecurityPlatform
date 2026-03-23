using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AgentPublicationRepository : RepositoryBase<AgentPublication>
{
    public AgentPublicationRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<AgentPublication>> GetByAgentIdAsync(
        TenantId tenantId,
        long agentId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<AgentPublication>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AgentId == agentId)
            .OrderBy(x => x.Version, OrderByType.Desc)
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToListAsync(cancellationToken);
    }

    public async Task<AgentPublication?> FindByAgentAndVersionAsync(
        TenantId tenantId,
        long agentId,
        int version,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<AgentPublication>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value &&
                x.AgentId == agentId &&
                x.Version == version)
            .FirstAsync(cancellationToken);
    }

    public async Task<AgentPublication?> FindActiveByAgentIdAsync(
        TenantId tenantId,
        long agentId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<AgentPublication>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value &&
                x.AgentId == agentId &&
                x.IsActive)
            .OrderBy(x => x.Version, OrderByType.Desc)
            .OrderBy(x => x.Id, OrderByType.Desc)
            .FirstAsync(cancellationToken);
    }

    public async Task<int> GetLatestVersionAsync(
        TenantId tenantId,
        long agentId,
        CancellationToken cancellationToken)
    {
        var latest = await Db.Ado.SqlQuerySingleAsync<long>(
            "SELECT IFNULL(MAX(Version), 0) AS Value FROM AgentPublication WHERE TenantIdValue = @tenantId AND AgentId = @agentId",
            new
            {
                tenantId = tenantId.Value.ToString(),
                agentId
            });
        if (latest >= int.MaxValue)
        {
            throw new OverflowException("Agent publication version exceeds Int32 range.");
        }

        return (int)latest;
    }

    public async Task DeactivateActiveByAgentIdAsync(
        TenantId tenantId,
        long agentId,
        CancellationToken cancellationToken)
    {
        var activeRecords = await Db.Queryable<AgentPublication>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AgentId == agentId && x.IsActive)
            .ToListAsync(cancellationToken);
        if (activeRecords.Count == 0)
        {
            return;
        }

        foreach (var record in activeRecords)
        {
            record.Deactivate();
        }

        await Db.Updateable(activeRecords)
            .WhereColumns(x => new { x.Id, x.TenantIdValue })
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<AgentPublication?> FindByEmbedTokenAsync(
        string embedToken,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<AgentPublication>()
            .Where(x => x.EmbedToken == embedToken && x.IsActive)
            .OrderBy(x => x.Id, OrderByType.Desc)
            .FirstAsync(cancellationToken);
    }

    public Task DeleteByAgentIdAsync(TenantId tenantId, long agentId, CancellationToken cancellationToken)
    {
        return Db.Deleteable<AgentPublication>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AgentId == agentId)
            .ExecuteCommandAsync(cancellationToken);
    }
}

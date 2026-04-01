using Atlas.Application.AgentTeam.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AgentTeam.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AgentTeamRepository : RepositoryBase<AgentTeamDefinition>, IAgentTeamRepository
{
    public AgentTeamRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<(IReadOnlyList<AgentTeamDefinition> Items, long Total)> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        TeamStatus? status,
        TeamPublishStatus? publishStatus,
        TeamRiskLevel? riskLevel,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<AgentTeamDefinition>()
            .Where(x => x.TenantIdValue == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalized = keyword.Trim();
            query = query.Where(x => x.TeamName.Contains(normalized) || (x.Description != null && x.Description.Contains(normalized)));
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (publishStatus.HasValue)
        {
            query = query.Where(x => x.PublishStatus == publishStatus.Value);
        }

        if (riskLevel.HasValue)
        {
            query = query.Where(x => x.RiskLevel == riskLevel.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (items, total);
    }

    public async Task<IReadOnlyList<AgentTeamDefinition>> GetByIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<AgentTeamDefinition>();
        }

        var array = ids.Distinct().ToArray();
        return await Db.Queryable<AgentTeamDefinition>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(array, x.Id))
            .ToListAsync(cancellationToken);
    }
}

public sealed class SubAgentRepository : RepositoryBase<SubAgentDefinition>, ISubAgentRepository
{
    public SubAgentRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<SubAgentDefinition>> GetByTeamIdAsync(TenantId tenantId, long teamId, CancellationToken cancellationToken)
    {
        return await Db.Queryable<SubAgentDefinition>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TeamId == teamId)
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .ToListAsync(cancellationToken);
    }
}

public sealed class OrchestrationNodeRepository : RepositoryBase<OrchestrationNodeDefinition>, IOrchestrationNodeRepository
{
    public OrchestrationNodeRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<OrchestrationNodeDefinition>> GetByTeamIdAsync(TenantId tenantId, long teamId, CancellationToken cancellationToken)
    {
        return await Db.Queryable<OrchestrationNodeDefinition>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TeamId == teamId)
            .OrderBy(x => x.Priority, OrderByType.Asc)
            .OrderBy(x => x.Id, OrderByType.Asc)
            .ToListAsync(cancellationToken);
    }
}

public sealed class TeamVersionRepository : RepositoryBase<TeamVersion>, ITeamVersionRepository
{
    public TeamVersionRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<TeamVersion>> GetByTeamIdAsync(TenantId tenantId, long teamId, CancellationToken cancellationToken)
    {
        return await Db.Queryable<TeamVersion>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TeamId == teamId)
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToListAsync(cancellationToken);
    }
}

public sealed class ExecutionRunRepository : RepositoryBase<ExecutionRun>, IExecutionRunRepository
{
    public ExecutionRunRepository(ISqlSugarClient db)
        : base(db)
    {
    }
}

public sealed class NodeRunRepository : RepositoryBase<NodeRun>, INodeRunRepository
{
    public NodeRunRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<NodeRun>> GetByRunIdAsync(TenantId tenantId, long runId, CancellationToken cancellationToken)
    {
        return await Db.Queryable<NodeRun>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.RunId == runId)
            .OrderBy(x => x.StartedAt, OrderByType.Asc)
            .OrderBy(x => x.Id, OrderByType.Asc)
            .ToListAsync(cancellationToken);
    }

    public Task AddRangeAsync(IReadOnlyList<NodeRun> entities, CancellationToken cancellationToken)
    {
        if (entities.Count == 0)
        {
            return Task.CompletedTask;
        }

        return Db.Insertable(entities.ToArray()).ExecuteCommandAsync(cancellationToken);
    }
}

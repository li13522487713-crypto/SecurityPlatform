using Atlas.Application.AgentTeam.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AgentTeam.Entities;

namespace Atlas.Application.AgentTeam.Abstractions;

public interface IAgentTeamRepository
{
    Task<(IReadOnlyList<AgentTeamDefinition> Items, long Total)> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        TeamStatus? status,
        TeamPublishStatus? publishStatus,
        TeamRiskLevel? riskLevel,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AgentTeamDefinition>> GetByIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken);

    Task<AgentTeamDefinition?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task AddAsync(AgentTeamDefinition entity, CancellationToken cancellationToken);
    Task UpdateAsync(AgentTeamDefinition entity, CancellationToken cancellationToken);
    Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
}

public interface ISubAgentRepository
{
    Task<IReadOnlyList<SubAgentDefinition>> GetByTeamIdAsync(TenantId tenantId, long teamId, CancellationToken cancellationToken);
    Task<SubAgentDefinition?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task AddAsync(SubAgentDefinition entity, CancellationToken cancellationToken);
    Task UpdateAsync(SubAgentDefinition entity, CancellationToken cancellationToken);
    Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
}

public interface IOrchestrationNodeRepository
{
    Task<IReadOnlyList<OrchestrationNodeDefinition>> GetByTeamIdAsync(TenantId tenantId, long teamId, CancellationToken cancellationToken);
    Task<OrchestrationNodeDefinition?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task AddAsync(OrchestrationNodeDefinition entity, CancellationToken cancellationToken);
    Task UpdateAsync(OrchestrationNodeDefinition entity, CancellationToken cancellationToken);
    Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
}

public interface ITeamVersionRepository
{
    Task<IReadOnlyList<TeamVersion>> GetByTeamIdAsync(TenantId tenantId, long teamId, CancellationToken cancellationToken);
    Task<TeamVersion?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task AddAsync(TeamVersion entity, CancellationToken cancellationToken);
}

public interface IExecutionRunRepository
{
    Task<ExecutionRun?> FindByIdAsync(TenantId tenantId, long runId, CancellationToken cancellationToken);
    Task AddAsync(ExecutionRun entity, CancellationToken cancellationToken);
    Task UpdateAsync(ExecutionRun entity, CancellationToken cancellationToken);
}

public interface INodeRunRepository
{
    Task<IReadOnlyList<NodeRun>> GetByRunIdAsync(TenantId tenantId, long runId, CancellationToken cancellationToken);
    Task<NodeRun?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task AddAsync(NodeRun entity, CancellationToken cancellationToken);
    Task AddRangeAsync(IReadOnlyList<NodeRun> entities, CancellationToken cancellationToken);
    Task UpdateAsync(NodeRun entity, CancellationToken cancellationToken);
}

public interface IAgentTeamQueryService
{
    Task<PagedResult<AgentTeamListItem>> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        TeamStatus? status,
        TeamPublishStatus? publishStatus,
        TeamRiskLevel? riskLevel,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<AgentTeamDetail?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task<IReadOnlyList<SubAgentItem>> GetSubAgentsAsync(TenantId tenantId, long teamId, CancellationToken cancellationToken);
    Task<IReadOnlyList<OrchestrationNodeItem>> GetNodesAsync(TenantId tenantId, long teamId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TeamVersionItem>> GetVersionsAsync(TenantId tenantId, long teamId, CancellationToken cancellationToken);
    Task<AgentTeamRunDetail?> GetRunAsync(TenantId tenantId, long runId, CancellationToken cancellationToken);
    Task<IReadOnlyList<NodeRunItem>> GetRunNodesAsync(TenantId tenantId, long runId, CancellationToken cancellationToken);
}

public interface IAgentTeamCommandService
{
    Task<long> CreateAsync(TenantId tenantId, AgentTeamCreateRequest request, CancellationToken cancellationToken);
    Task UpdateAsync(TenantId tenantId, long id, AgentTeamUpdateRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task<long> DuplicateAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task DisableAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task EnableAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task<long> PublishAsync(TenantId tenantId, long id, string publisher, TeamPublishRequest request, CancellationToken cancellationToken);
    Task RollbackAsync(TenantId tenantId, long id, long versionId, string publisher, CancellationToken cancellationToken);

    Task<long> CreateSubAgentAsync(TenantId tenantId, long teamId, SubAgentCreateRequest request, CancellationToken cancellationToken);
    Task UpdateSubAgentAsync(TenantId tenantId, long teamId, long subAgentId, SubAgentUpdateRequest request, CancellationToken cancellationToken);
    Task DeleteSubAgentAsync(TenantId tenantId, long teamId, long subAgentId, CancellationToken cancellationToken);

    Task<long> CreateNodeAsync(TenantId tenantId, long teamId, OrchestrationNodeCreateRequest request, CancellationToken cancellationToken);
    Task UpdateNodeAsync(TenantId tenantId, long teamId, long nodeId, OrchestrationNodeUpdateRequest request, CancellationToken cancellationToken);
    Task DeleteNodeAsync(TenantId tenantId, long teamId, long nodeId, CancellationToken cancellationToken);
    Task ValidateOrchestrationAsync(TenantId tenantId, long teamId, CancellationToken cancellationToken);

    Task<long> CreateRunAsync(TenantId tenantId, string triggerBy, AgentTeamRunCreateRequest request, CancellationToken cancellationToken);
    Task CancelRunAsync(TenantId tenantId, long runId, CancellationToken cancellationToken);
    Task InterveneRunAsync(TenantId tenantId, long runId, long nodeRunId, AgentTeamRunInterveneRequest request, CancellationToken cancellationToken);
    Task<AgentTeamDebugResult> DebugAsync(TenantId tenantId, long teamId, AgentTeamDebugRequest request, CancellationToken cancellationToken);
}

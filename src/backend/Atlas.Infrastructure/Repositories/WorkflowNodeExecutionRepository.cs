using Atlas.Application.AiPlatform.Repositories;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Services;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class WorkflowNodeExecutionRepository : IWorkflowNodeExecutionRepository
{
    private readonly ISqlSugarClient _mainDb;

    public WorkflowNodeExecutionRepository(
        ISqlSugarClient db,
        IAppDbScopeFactory appDbScopeFactory,
        IAppContextAccessor appContextAccessor)
    {
        _mainDb = db;
    }

    public WorkflowNodeExecutionRepository(ISqlSugarClient db)
        : this(db, new MainOnlyAppDbScopeFactory(db), NullAppContextAccessor.Instance)
    {
    }

    public async Task<IReadOnlyList<WorkflowNodeExecution>> ListByExecutionIdAsync(
        TenantId tenantId, long executionId, CancellationToken cancellationToken)
    {
        return await _mainDb.Queryable<WorkflowNodeExecution>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ExecutionId == executionId)
            .OrderBy(x => x.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkflowNodeExecution?> FindByNodeKeyAsync(
        TenantId tenantId, long executionId, string nodeKey, CancellationToken cancellationToken)
    {
        return await _mainDb.Queryable<WorkflowNodeExecution>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ExecutionId == executionId && x.NodeKey == nodeKey)
            .FirstAsync(cancellationToken);
    }

    public async Task AddAsync(WorkflowNodeExecution entity, CancellationToken cancellationToken)
    {
        await _mainDb.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task BatchAddAsync(IReadOnlyList<WorkflowNodeExecution> entities, CancellationToken cancellationToken)
    {
        if (entities.Count == 0)
        {
            return;
        }

        await _mainDb.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateAsync(WorkflowNodeExecution entity, CancellationToken cancellationToken)
    {
        await _mainDb.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }
}

using Atlas.Application.AiPlatform.Repositories;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Infrastructure.Services;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class WorkflowExecutionRepository : RepositoryBase<WorkflowExecution>, IWorkflowExecutionRepository
{
    private readonly ISqlSugarClient _mainDb;

    public WorkflowExecutionRepository(
        ISqlSugarClient db,
        IAppDbScopeFactory appDbScopeFactory,
        IAppContextAccessor appContextAccessor) : base(db)
    {
        _mainDb = db;
    }

    public WorkflowExecutionRepository(ISqlSugarClient db)
        : this(db, new MainOnlyAppDbScopeFactory(db), NullAppContextAccessor.Instance)
    {
    }

    public new async Task<WorkflowExecution?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return await _mainDb.Queryable<WorkflowExecution>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public override async Task AddAsync(WorkflowExecution entity, CancellationToken cancellationToken)
    {
        await _mainDb.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public override async Task UpdateAsync(WorkflowExecution entity, CancellationToken cancellationToken)
    {
        await _mainDb.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<int> DeleteCompletedBeforeAsync(DateTime before, int maxRows, CancellationToken cancellationToken)
    {
        var terminalStatuses = new[]
        {
            ExecutionStatus.Completed,
            ExecutionStatus.Failed,
            ExecutionStatus.Cancelled
        };

        var toDelete = await _mainDb.Queryable<WorkflowExecution>()
            .Where(x => terminalStatuses.Contains(x.Status) && x.CompletedAt < before)
            .Take(maxRows)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (toDelete.Count == 0) return 0;

        return await _mainDb.Deleteable<WorkflowExecution>()
            .Where(x => toDelete.Contains(x.Id))
            .ExecuteCommandAsync(cancellationToken);
    }
}

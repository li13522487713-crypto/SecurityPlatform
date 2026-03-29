using Atlas.Application.AiPlatform.Repositories;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.LowCode.Entities;
using Atlas.Infrastructure.Services;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class WorkflowNodeExecutionRepository : IWorkflowNodeExecutionRepository
{
    private readonly ISqlSugarClient _mainDb;
    private readonly IAppDbScopeFactory _appDbScopeFactory;
    private readonly IAppContextAccessor _appContextAccessor;

    public WorkflowNodeExecutionRepository(
        ISqlSugarClient db,
        IAppDbScopeFactory appDbScopeFactory,
        IAppContextAccessor appContextAccessor)
    {
        _mainDb = db;
        _appDbScopeFactory = appDbScopeFactory;
        _appContextAccessor = appContextAccessor;
    }

    public WorkflowNodeExecutionRepository(ISqlSugarClient db)
        : this(db, new MainOnlyAppDbScopeFactory(db), NullAppContextAccessor.Instance)
    {
    }

    public async Task<IReadOnlyList<WorkflowNodeExecution>> ListByExecutionIdAsync(
        TenantId tenantId, long executionId, CancellationToken cancellationToken)
    {
        var db = await ResolveDbByExecutionIdAsync(tenantId, executionId, cancellationToken);
        return await db.Queryable<WorkflowNodeExecution>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ExecutionId == executionId)
            .OrderBy(x => x.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkflowNodeExecution?> FindByNodeKeyAsync(
        TenantId tenantId, long executionId, string nodeKey, CancellationToken cancellationToken)
    {
        var db = await ResolveDbByExecutionIdAsync(tenantId, executionId, cancellationToken);
        return await db.Queryable<WorkflowNodeExecution>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ExecutionId == executionId && x.NodeKey == nodeKey)
            .FirstAsync(cancellationToken);
    }

    public async Task AddAsync(WorkflowNodeExecution entity, CancellationToken cancellationToken)
    {
        var db = await ResolveDbForWriteAsync(new TenantId(entity.TenantIdValue), cancellationToken);
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task BatchAddAsync(IReadOnlyList<WorkflowNodeExecution> entities, CancellationToken cancellationToken)
    {
        if (entities.Count == 0)
        {
            return;
        }

        var db = await ResolveDbForWriteAsync(new TenantId(entities[0].TenantIdValue), cancellationToken);
        await db.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateAsync(WorkflowNodeExecution entity, CancellationToken cancellationToken)
    {
        var db = await ResolveDbByExecutionIdAsync(new TenantId(entity.TenantIdValue), entity.ExecutionId, cancellationToken);
        await db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }

    private async Task<ISqlSugarClient> ResolveDbForWriteAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var appId = _appContextAccessor.ResolveAppId();
        if (appId.HasValue && appId.Value > 0)
        {
            return await _appDbScopeFactory.GetAppClientAsync(tenantId, appId.Value, cancellationToken);
        }

        return _mainDb;
    }

    private async Task<ISqlSugarClient> ResolveDbByExecutionIdAsync(
        TenantId tenantId,
        long executionId,
        CancellationToken cancellationToken)
    {
        var appId = _appContextAccessor.ResolveAppId();
        if (appId.HasValue && appId.Value > 0)
        {
            return await _appDbScopeFactory.GetAppClientAsync(tenantId, appId.Value, cancellationToken);
        }

        var mainExecutionExists = await _mainDb.Queryable<WorkflowExecution>()
            .AnyAsync(x => x.TenantIdValue == tenantId.Value && x.Id == executionId, cancellationToken);
        if (mainExecutionExists)
        {
            return _mainDb;
        }

        var appIds = await _mainDb.Queryable<LowCodeApp>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var lookupTasks = appIds.Select(async item =>
        {
            var appDb = await _appDbScopeFactory.GetAppClientAsync(tenantId, item, cancellationToken);
            var exists = await appDb.Queryable<WorkflowExecution>()
                .AnyAsync(x => x.TenantIdValue == tenantId.Value && x.Id == executionId, cancellationToken);
            return (AppId: item, Exists: exists);
        }).ToArray();
        var lookupResults = await Task.WhenAll(lookupTasks);
        var match = lookupResults.FirstOrDefault(item => item.Exists);
        if (match.Exists)
        {
            return await _appDbScopeFactory.GetAppClientAsync(tenantId, match.AppId, cancellationToken);
        }

        return _mainDb;
    }
}

using Atlas.Application.AiPlatform.Repositories;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.LowCode.Entities;
using Atlas.Infrastructure.Services;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class WorkflowExecutionRepository : RepositoryBase<WorkflowExecution>, IWorkflowExecutionRepository
{
    private readonly ISqlSugarClient _mainDb;
    private readonly IAppDbScopeFactory _appDbScopeFactory;
    private readonly IAppContextAccessor _appContextAccessor;

    public WorkflowExecutionRepository(
        ISqlSugarClient db,
        IAppDbScopeFactory appDbScopeFactory,
        IAppContextAccessor appContextAccessor) : base(db)
    {
        _mainDb = db;
        _appDbScopeFactory = appDbScopeFactory;
        _appContextAccessor = appContextAccessor;
    }

    public WorkflowExecutionRepository(ISqlSugarClient db)
        : this(db, new MainOnlyAppDbScopeFactory(db), NullAppContextAccessor.Instance)
    {
    }

    public new async Task<WorkflowExecution?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var appId = _appContextAccessor.ResolveAppId();
        if (appId.HasValue && appId.Value > 0)
        {
            var appDb = await _appDbScopeFactory.GetAppClientAsync(tenantId, appId.Value, cancellationToken);
            return await appDb.Queryable<WorkflowExecution>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
                .FirstAsync(cancellationToken);
        }

        var execution = await _mainDb.Queryable<WorkflowExecution>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
        if (execution is not null)
        {
            return execution;
        }

        var appIds = await _mainDb.Queryable<LowCodeApp>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var lookupTasks = appIds.Select(async item =>
        {
            var appDb = await _appDbScopeFactory.GetAppClientAsync(tenantId, item, cancellationToken);
            var row = await appDb.Queryable<WorkflowExecution>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
                .FirstAsync(cancellationToken);
            return row;
        }).ToArray();
        var rows = await Task.WhenAll(lookupTasks);
        return rows.FirstOrDefault(x => x is not null);
    }

    public override async Task AddAsync(WorkflowExecution entity, CancellationToken cancellationToken)
    {
        var db = await ResolveDbForWriteAsync(new TenantId(entity.TenantIdValue), entity.AppId, cancellationToken);
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public override async Task UpdateAsync(WorkflowExecution entity, CancellationToken cancellationToken)
    {
        var db = await ResolveDbForWriteAsync(new TenantId(entity.TenantIdValue), entity.AppId, cancellationToken);
        await db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }

    private async Task<ISqlSugarClient> ResolveDbForWriteAsync(
        TenantId tenantId,
        long? executionAppId,
        CancellationToken cancellationToken)
    {
        var appId = executionAppId ?? _appContextAccessor.ResolveAppId();
        if (appId.HasValue && appId.Value > 0)
        {
            return await _appDbScopeFactory.GetAppClientAsync(tenantId, appId.Value, cancellationToken);
        }

        return _mainDb;
    }
}

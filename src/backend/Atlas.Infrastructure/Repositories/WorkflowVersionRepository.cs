using Atlas.Application.AiPlatform.Repositories;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Services;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class WorkflowVersionRepository : RepositoryBase<WorkflowVersion>, IWorkflowVersionRepository
{
    private readonly ISqlSugarClient _mainDb;
    private readonly IAppDbScopeFactory _appDbScopeFactory;
    private readonly IAppContextAccessor _appContextAccessor;

    public WorkflowVersionRepository(
        ISqlSugarClient db,
        IAppDbScopeFactory appDbScopeFactory,
        IAppContextAccessor appContextAccessor) : base(db)
    {
        _mainDb = db;
        _appDbScopeFactory = appDbScopeFactory;
        _appContextAccessor = appContextAccessor;
    }

    public WorkflowVersionRepository(ISqlSugarClient db)
        : this(db, new MainOnlyAppDbScopeFactory(db), NullAppContextAccessor.Instance)
    {
    }

    public async Task<IReadOnlyList<WorkflowVersion>> ListByWorkflowIdAsync(
        TenantId tenantId, long workflowId, CancellationToken cancellationToken)
    {
        var db = await ResolveDbAsync(tenantId, cancellationToken);
        return await db.Queryable<WorkflowVersion>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.WorkflowId == workflowId)
            .OrderBy(x => x.VersionNumber, OrderByType.Desc)
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkflowVersion?> GetLatestAsync(TenantId tenantId, long workflowId, CancellationToken cancellationToken)
    {
        var db = await ResolveDbAsync(tenantId, cancellationToken);
        return await db.Queryable<WorkflowVersion>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.WorkflowId == workflowId)
            .OrderBy(x => x.VersionNumber, OrderByType.Desc)
            .FirstAsync(cancellationToken);
    }

    public override async Task<WorkflowVersion?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var db = await ResolveDbAsync(tenantId, cancellationToken);
        return await db.Queryable<WorkflowVersion>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public override async Task AddAsync(WorkflowVersion entity, CancellationToken cancellationToken)
    {
        var db = await ResolveDbAsync(new TenantId(entity.TenantIdValue), cancellationToken);
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    private async Task<ISqlSugarClient> ResolveDbAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var appId = _appContextAccessor.ResolveAppId();
        if (appId.HasValue && appId.Value > 0)
        {
            return await _appDbScopeFactory.GetAppClientAsync(tenantId, appId.Value, cancellationToken);
        }

        return _mainDb;
    }
}

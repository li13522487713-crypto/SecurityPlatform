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

    public WorkflowVersionRepository(
        ISqlSugarClient db,
        IAppDbScopeFactory appDbScopeFactory,
        IAppContextAccessor appContextAccessor) : base(db)
    {
        _mainDb = db;
    }

    public WorkflowVersionRepository(ISqlSugarClient db)
        : this(db, new MainOnlyAppDbScopeFactory(db), NullAppContextAccessor.Instance)
    {
    }

    public async Task<IReadOnlyList<WorkflowVersion>> ListByWorkflowIdAsync(
        TenantId tenantId, long workflowId, CancellationToken cancellationToken)
    {
        return await _mainDb.Queryable<WorkflowVersion>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.WorkflowId == workflowId)
            .OrderBy(x => x.VersionNumber, OrderByType.Desc)
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkflowVersion?> GetLatestAsync(TenantId tenantId, long workflowId, CancellationToken cancellationToken)
    {
        return await _mainDb.Queryable<WorkflowVersion>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.WorkflowId == workflowId)
            .OrderBy(x => x.VersionNumber, OrderByType.Desc)
            .FirstAsync(cancellationToken);
    }

    public override async Task<WorkflowVersion?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return await _mainDb.Queryable<WorkflowVersion>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<WorkflowVersion?> FindByWorkflowAndVersionNumberAsync(
        TenantId tenantId,
        long workflowId,
        int versionNumber,
        CancellationToken cancellationToken)
    {
        return await _mainDb.Queryable<WorkflowVersion>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value &&
                x.WorkflowId == workflowId &&
                x.VersionNumber == versionNumber)
            .FirstAsync(cancellationToken);
    }

    public override async Task AddAsync(WorkflowVersion entity, CancellationToken cancellationToken)
    {
        await _mainDb.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }
}

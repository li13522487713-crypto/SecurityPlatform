using Atlas.Application.AiPlatform.Repositories;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Services;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class CozeWorkflowVersionRepository : RepositoryBase<CozeWorkflowVersion>, ICozeWorkflowVersionRepository
{
    private readonly ISqlSugarClient _mainDb;

    public CozeWorkflowVersionRepository(
        ISqlSugarClient db,
        IAppDbScopeFactory appDbScopeFactory,
        IAppContextAccessor appContextAccessor) : base(db)
    {
        _mainDb = db;
    }

    public CozeWorkflowVersionRepository(ISqlSugarClient db)
        : this(db, new MainOnlyAppDbScopeFactory(db), NullAppContextAccessor.Instance)
    {
    }

    public async Task<IReadOnlyList<CozeWorkflowVersion>> ListByWorkflowIdAsync(
        TenantId tenantId, long workflowId, CancellationToken cancellationToken)
    {
        return await _mainDb.Queryable<CozeWorkflowVersion>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.WorkflowId == workflowId)
            .OrderBy(x => x.VersionNumber, OrderByType.Desc)
            .ToListAsync(cancellationToken);
    }

    public async Task<CozeWorkflowVersion?> GetLatestAsync(TenantId tenantId, long workflowId, CancellationToken cancellationToken)
    {
        return await _mainDb.Queryable<CozeWorkflowVersion>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.WorkflowId == workflowId)
            .OrderBy(x => x.VersionNumber, OrderByType.Desc)
            .FirstAsync(cancellationToken);
    }

    public override async Task<CozeWorkflowVersion?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return await _mainDb.Queryable<CozeWorkflowVersion>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<CozeWorkflowVersion?> FindByWorkflowAndVersionNumberAsync(
        TenantId tenantId,
        long workflowId,
        int versionNumber,
        CancellationToken cancellationToken)
    {
        return await _mainDb.Queryable<CozeWorkflowVersion>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.WorkflowId == workflowId && x.VersionNumber == versionNumber)
            .FirstAsync(cancellationToken);
    }

    public override async Task AddAsync(CozeWorkflowVersion entity, CancellationToken cancellationToken)
    {
        await _mainDb.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }
}

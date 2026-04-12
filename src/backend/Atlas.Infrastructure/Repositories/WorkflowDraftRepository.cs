using Atlas.Application.AiPlatform.Repositories;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Services;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class WorkflowDraftRepository : RepositoryBase<WorkflowDraft>, IWorkflowDraftRepository
{
    private readonly ISqlSugarClient _mainDb;

    public WorkflowDraftRepository(
        ISqlSugarClient db,
        IAppDbScopeFactory appDbScopeFactory,
        IAppContextAccessor appContextAccessor) : base(db)
    {
        _mainDb = db;
    }

    public WorkflowDraftRepository(ISqlSugarClient db)
        : this(db, new MainOnlyAppDbScopeFactory(db), NullAppContextAccessor.Instance)
    {
    }

    public async Task<WorkflowDraft?> FindByWorkflowIdAsync(TenantId tenantId, long workflowId, CancellationToken cancellationToken)
    {
        return await _mainDb.Queryable<WorkflowDraft>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.WorkflowId == workflowId)
            .FirstAsync(cancellationToken);
    }

    public override async Task AddAsync(WorkflowDraft entity, CancellationToken cancellationToken)
    {
        await _mainDb.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public override async Task UpdateAsync(WorkflowDraft entity, CancellationToken cancellationToken)
    {
        await _mainDb.Updateable<WorkflowDraft>()
            .SetColumns(x => x.CanvasJson == entity.CanvasJson)
            .SetColumns(x => x.CommitId == entity.CommitId)
            .SetColumns(x => x.UpdatedAt == entity.UpdatedAt)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }
}

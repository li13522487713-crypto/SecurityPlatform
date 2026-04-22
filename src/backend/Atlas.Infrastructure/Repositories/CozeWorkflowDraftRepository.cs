using Atlas.Application.AiPlatform.Repositories;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Services;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class CozeWorkflowDraftRepository : RepositoryBase<CozeWorkflowDraft>, ICozeWorkflowDraftRepository
{
    private readonly ISqlSugarClient _mainDb;

    public CozeWorkflowDraftRepository(
        ISqlSugarClient db,
        IAppDbScopeFactory appDbScopeFactory,
        IAppContextAccessor appContextAccessor) : base(db)
    {
        _mainDb = db;
    }

    public CozeWorkflowDraftRepository(ISqlSugarClient db)
        : this(db, new MainOnlyAppDbScopeFactory(db), NullAppContextAccessor.Instance)
    {
    }

    public async Task<CozeWorkflowDraft?> FindByWorkflowIdAsync(TenantId tenantId, long workflowId, CancellationToken cancellationToken)
    {
        return await _mainDb.Queryable<CozeWorkflowDraft>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.WorkflowId == workflowId)
            .FirstAsync(cancellationToken);
    }

    public override async Task AddAsync(CozeWorkflowDraft entity, CancellationToken cancellationToken)
    {
        await _mainDb.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public override async Task UpdateAsync(CozeWorkflowDraft entity, CancellationToken cancellationToken)
    {
        await _mainDb.Updateable<CozeWorkflowDraft>()
            .SetColumns(x => x.SchemaJson == entity.SchemaJson)
            .SetColumns(x => x.CommitId == entity.CommitId)
            .SetColumns(x => x.UpdatedAt == entity.UpdatedAt)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }
}

using Atlas.Application.AiPlatform.Repositories;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
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

    /// <inheritdoc />
    public async Task UpdateAsync(CozeWorkflowDraft entity, string? oldCommitId, CancellationToken cancellationToken)
    {
        var query = _mainDb.Updateable<CozeWorkflowDraft>()
            .SetColumns(x => x.SchemaJson == entity.SchemaJson)
            .SetColumns(x => x.CommitId == entity.CommitId)
            .SetColumns(x => x.UpdatedAt == entity.UpdatedAt)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue);

        // Optimistic lock: only update if the stored commit_id matches the old value (or is null)
        if (!string.IsNullOrWhiteSpace(oldCommitId))
        {
            query = query.Where(x => x.CommitId == oldCommitId || x.CommitId == null);
        }

        var affected = await query.ExecuteCommandAsync(cancellationToken);

        if (affected == 0)
        {
            throw new BusinessException(
                "并发冲突：画布已被其他会话修改，请刷新后重试。",
                ErrorCodes.Conflict);
        }
    }

    /// <summary>
    /// Compatibility override — delegates without optimistic locking.
    /// </summary>
    public override Task UpdateAsync(CozeWorkflowDraft entity, CancellationToken cancellationToken)
        => UpdateAsync(entity, oldCommitId: null, cancellationToken);
}

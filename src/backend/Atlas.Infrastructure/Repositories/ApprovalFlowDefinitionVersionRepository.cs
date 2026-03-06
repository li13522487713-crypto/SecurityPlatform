using Atlas.Application.Approval.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class ApprovalFlowDefinitionVersionRepository : IApprovalFlowDefinitionVersionRepository
{
    private readonly ISqlSugarClient _db;

    public ApprovalFlowDefinitionVersionRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<ApprovalFlowDefinitionVersion?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken = default)
    {
        return await _db.Queryable<ApprovalFlowDefinitionVersion>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalFlowDefinitionVersion>> GetByDefinitionIdAsync(
        TenantId tenantId,
        long definitionId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Queryable<ApprovalFlowDefinitionVersion>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DefinitionId == definitionId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task InsertAsync(ApprovalFlowDefinitionVersion entity, CancellationToken cancellationToken = default)
    {
        return _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteByDefinitionIdAsync(
        TenantId tenantId,
        long definitionId,
        CancellationToken cancellationToken = default)
    {
        return _db.Deleteable<ApprovalFlowDefinitionVersion>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DefinitionId == definitionId)
            .ExecuteCommandAsync(cancellationToken);
    }
}

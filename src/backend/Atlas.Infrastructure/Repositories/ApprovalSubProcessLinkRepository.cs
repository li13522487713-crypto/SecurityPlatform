using Atlas.Application.Approval.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

/// <summary>
/// 子流程关联记录仓储实现
/// </summary>
public sealed class ApprovalSubProcessLinkRepository : IApprovalSubProcessLinkRepository
{
    private readonly ISqlSugarClient _db;

    public ApprovalSubProcessLinkRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task AddAsync(ApprovalSubProcessLink entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateAsync(ApprovalSubProcessLink entity, CancellationToken cancellationToken)
    {
        await _db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<ApprovalSubProcessLink?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalSubProcessLink>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<ApprovalSubProcessLink?> GetByChildInstanceIdAsync(
        TenantId tenantId,
        long childInstanceId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalSubProcessLink>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ChildInstanceId == childInstanceId)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalSubProcessLink>> GetByParentInstanceIdAsync(
        TenantId tenantId,
        long parentInstanceId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalSubProcessLink>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ParentInstanceId == parentInstanceId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasActiveSubProcessAsync(
        TenantId tenantId,
        long parentInstanceId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalSubProcessLink>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.ParentInstanceId == parentInstanceId
                && x.Status == 0)
            .AnyAsync();
    }
}

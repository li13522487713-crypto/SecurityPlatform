using Atlas.Application.Approval.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

/// <summary>
/// 审批任务仓储实现
/// </summary>
public sealed class ApprovalTaskRepository : IApprovalTaskRepository
{
    private readonly ISqlSugarClient _db;

    public ApprovalTaskRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task AddAsync(ApprovalTask entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<ApprovalTask> entities, CancellationToken cancellationToken)
    {
        await _db.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateAsync(ApprovalTask entity, CancellationToken cancellationToken)
    {
        await _db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateRangeAsync(IEnumerable<ApprovalTask> entities, CancellationToken cancellationToken)
    {
        await _db.Updateable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<ApprovalTask?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalTask>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<ApprovalTask> Items, int TotalCount)> GetPagedByInstanceAsync(
        TenantId tenantId,
        long instanceId,
        int pageIndex,
        int pageSize,
        ApprovalTaskStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Queryable<ApprovalTask>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.InstanceId == instanceId);

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<ApprovalTask> Items, int TotalCount)> GetPagedByAssigneeAsync(
        TenantId tenantId,
        long userId,
        int pageIndex,
        int pageSize,
        ApprovalTaskStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Queryable<ApprovalTask>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.AssigneeType == AssigneeType.User
                && x.AssigneeValue == userId.ToString());

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<ApprovalTask>> GetByInstanceAndNodeAsync(
        TenantId tenantId,
        long instanceId,
        string nodeId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalTask>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.InstanceId == instanceId
                && x.NodeId == nodeId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalTask>> GetByInstanceAndStatusAsync(
        TenantId tenantId,
        long instanceId,
        ApprovalTaskStatus status,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalTask>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.InstanceId == instanceId
                && x.Status == status)
            .ToListAsync(cancellationToken);
    }
}

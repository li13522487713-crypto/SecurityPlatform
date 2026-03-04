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
        await _db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateRangeAsync(IEnumerable<ApprovalTask> entities, CancellationToken cancellationToken)
    {
        await _db.Updateable(entities.ToList())
            .WhereColumns(x => new { x.Id, x.TenantIdValue })
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<ApprovalTask?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalTask>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalTask>> QueryByIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<ApprovalTask>();
        }

        var distinctIds = ids.Distinct().ToArray();
        return await _db.Queryable<ApprovalTask>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(distinctIds, x.Id))
            .ToListAsync(cancellationToken);
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

    public async Task<(IReadOnlyList<ApprovalTask> Items, int TotalCount)> GetPagedPoolAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Queryable<ApprovalTask>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.Status == ApprovalTaskStatus.Pending
                && x.AssigneeType != AssigneeType.User);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (items, totalCount);
    }

    public async Task<IReadOnlyList<ApprovalTask>> GetPendingByAssigneeUserAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalTask>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.AssigneeType == AssigneeType.User
                && x.AssigneeValue == userId.ToString()
                && x.Status == ApprovalTaskStatus.Pending)
            .ToListAsync(cancellationToken);
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

    public async Task<IReadOnlyList<ApprovalTask>> GetByInstanceAndNodesAsync(
        TenantId tenantId,
        long instanceId,
        IReadOnlyList<string> nodeIds,
        CancellationToken cancellationToken)
    {
        if (nodeIds.Count == 0)
        {
            return Array.Empty<ApprovalTask>();
        }

        var distinctNodeIds = nodeIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (distinctNodeIds.Length == 0)
        {
            return Array.Empty<ApprovalTask>();
        }

        return await _db.Queryable<ApprovalTask>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.InstanceId == instanceId
                && SqlFunc.ContainsArray(distinctNodeIds, x.NodeId))
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

    public async Task<bool> ExistsByInstanceAndAssigneeAsync(
        TenantId tenantId,
        long instanceId,
        long assigneeUserId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalTask>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.InstanceId == instanceId
                && x.AssigneeType == AssigneeType.User
                && x.AssigneeValue == assigneeUserId.ToString())
            .AnyAsync();
    }

    public async Task<int> CountByStatusAsync(
        TenantId tenantId,
        ApprovalTaskStatus status,
        DateTimeOffset? createdBefore,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<ApprovalTask>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Status == status);

        if (createdBefore.HasValue)
        {
            var threshold = createdBefore.Value;
            query = query.Where(x => x.CreatedAt <= threshold);
        }

        return await query.CountAsync(cancellationToken);
    }
}

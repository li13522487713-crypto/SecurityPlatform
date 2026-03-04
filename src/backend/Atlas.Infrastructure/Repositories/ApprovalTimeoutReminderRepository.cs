using Atlas.Application.Approval.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

/// <summary>
/// 超时提醒记录仓储实现
/// </summary>
public sealed class ApprovalTimeoutReminderRepository : IApprovalTimeoutReminderRepository
{
    private readonly ISqlSugarClient _db;

    public ApprovalTimeoutReminderRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task AddAsync(ApprovalTimeoutReminder entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<ApprovalTimeoutReminder> entities, CancellationToken cancellationToken)
    {
        await _db.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateAsync(ApprovalTimeoutReminder entity, CancellationToken cancellationToken)
    {
        await _db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateRangeAsync(IEnumerable<ApprovalTimeoutReminder> entities, CancellationToken cancellationToken)
    {
        await _db.Updateable(entities.ToList())
            .WhereColumns(x => new { x.Id, x.TenantIdValue })
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<ApprovalTimeoutReminder?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalTimeoutReminder>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalTimeoutReminder>> GetPendingRemindersAsync(
        TenantId tenantId,
        DateTimeOffset currentTime,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalTimeoutReminder>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && !x.IsCompleted
                && x.ExpectedCompleteTime <= currentTime)
            .OrderBy(x => x.ExpectedCompleteTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<ApprovalTimeoutReminder?> GetByInstanceAndTaskAsync(
        TenantId tenantId,
        long instanceId,
        long? taskId,
        string nodeId,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<ApprovalTimeoutReminder>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.InstanceId == instanceId
                && x.NodeId == nodeId);

        if (taskId.HasValue)
        {
            query = query.Where(x => x.TaskId == taskId.Value);
        }
        else
        {
            query = query.Where(x => x.TaskId == null);
        }

        return await query.FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalTimeoutReminder>> GetByInstanceAsync(
        TenantId tenantId,
        long instanceId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalTimeoutReminder>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.InstanceId == instanceId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalTimeoutReminder>> GetByInstancesAsync(
        TenantId tenantId,
        IReadOnlyList<long> instanceIds,
        CancellationToken cancellationToken)
    {
        if (instanceIds.Count == 0)
        {
            return Array.Empty<ApprovalTimeoutReminder>();
        }

        var distinctIds = instanceIds.Distinct().ToArray();
        return await _db.Queryable<ApprovalTimeoutReminder>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && SqlFunc.ContainsArray(distinctIds, x.InstanceId))
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalTimeoutReminder>> GetByInstanceAndNodeAsync(
        TenantId tenantId,
        long instanceId,
        string nodeId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalTimeoutReminder>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.InstanceId == instanceId
                && x.NodeId == nodeId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}

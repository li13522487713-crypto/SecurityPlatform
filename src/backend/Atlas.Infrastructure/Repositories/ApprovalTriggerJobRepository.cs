using Atlas.Application.Approval.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

/// <summary>
/// 审批触发器任务仓储实现
/// </summary>
public sealed class ApprovalTriggerJobRepository : IApprovalTriggerJobRepository
{
    private readonly ISqlSugarClient _db;

    public ApprovalTriggerJobRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task AddAsync(ApprovalTriggerJob entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateAsync(ApprovalTriggerJob entity, CancellationToken cancellationToken)
    {
        await _db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<ApprovalTriggerJob?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalTriggerJob>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalTriggerJob>> GetPendingDueJobsAsync(
        TenantId tenantId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalTriggerJob>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.Status == 0
                && x.ScheduledAt <= now)
            .OrderBy(x => x.ScheduledAt, OrderByType.Asc)
            .ToListAsync(cancellationToken);
    }

    public async Task<ApprovalTriggerJob?> GetByInstanceAndNodeAsync(
        TenantId tenantId,
        long instanceId,
        string nodeId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalTriggerJob>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.InstanceId == instanceId
                && x.NodeId == nodeId)
            .FirstAsync(cancellationToken);
    }
}

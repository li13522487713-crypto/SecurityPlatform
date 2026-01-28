using Atlas.Application.Approval.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

/// <summary>
/// 审批任务加签/减签记录仓储实现
/// </summary>
public sealed class ApprovalTaskAssigneeChangeRepository : IApprovalTaskAssigneeChangeRepository
{
    private readonly ISqlSugarClient _db;

    public ApprovalTaskAssigneeChangeRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task AddAsync(ApprovalTaskAssigneeChange entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalTaskAssigneeChange>> GetByInstanceAndNodeAsync(
        TenantId tenantId,
        long instanceId,
        string nodeId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalTaskAssigneeChange>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.InstanceId == instanceId
                && x.NodeId == nodeId)
            .OrderByDescending(x => x.ChangedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalTaskAssigneeChange>> GetByInstanceAsync(
        TenantId tenantId,
        long instanceId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalTaskAssigneeChange>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.InstanceId == instanceId)
            .OrderByDescending(x => x.ChangedAt)
            .ToListAsync(cancellationToken);
    }
}

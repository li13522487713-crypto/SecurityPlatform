using Atlas.Application.Approval.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

/// <summary>
/// 审批任务转办记录仓储实现
/// </summary>
public sealed class ApprovalTaskTransferRepository : IApprovalTaskTransferRepository
{
    private readonly ISqlSugarClient _db;

    public ApprovalTaskTransferRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task AddAsync(ApprovalTaskTransfer entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalTaskTransfer>> GetByTaskIdAsync(
        TenantId tenantId,
        long taskId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalTaskTransfer>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TaskId == taskId)
            .OrderByDescending(x => x.TransferredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalTaskTransfer>> GetByInstanceAsync(
        TenantId tenantId,
        long instanceId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalTaskTransfer>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.InstanceId == instanceId)
            .OrderByDescending(x => x.TransferredAt)
            .ToListAsync(cancellationToken);
    }
}

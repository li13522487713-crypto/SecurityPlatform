using Atlas.Application.Approval.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

/// <summary>
/// 审批节点执行记录仓储实现
/// </summary>
public sealed class ApprovalNodeExecutionRepository : IApprovalNodeExecutionRepository
{
    private readonly ISqlSugarClient _db;

    public ApprovalNodeExecutionRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task AddAsync(ApprovalNodeExecution entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateAsync(ApprovalNodeExecution entity, CancellationToken cancellationToken)
    {
        await _db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<ApprovalNodeExecution?> GetByInstanceAndNodeAsync(
        TenantId tenantId,
        long instanceId,
        string nodeId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalNodeExecution>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.InstanceId == instanceId
                && x.NodeId == nodeId)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalNodeExecution>> GetByInstanceAsync(
        TenantId tenantId,
        long instanceId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalNodeExecution>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.InstanceId == instanceId)
            .OrderBy(x => x.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalNodeExecution>> GetByInstanceAndStatusAsync(
        TenantId tenantId,
        long instanceId,
        ApprovalNodeExecutionStatus status,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalNodeExecution>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.InstanceId == instanceId
                && x.Status == status)
            .OrderBy(x => x.StartedAt)
            .ToListAsync(cancellationToken);
    }
}

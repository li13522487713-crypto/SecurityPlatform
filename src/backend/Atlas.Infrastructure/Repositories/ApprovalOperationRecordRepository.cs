using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

/// <summary>
/// 审批操作记录仓储实现
/// </summary>
public sealed class ApprovalOperationRecordRepository : IApprovalOperationRecordRepository
{
    private readonly ISqlSugarClient _db;

    public ApprovalOperationRecordRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public Task AddAsync(ApprovalOperationRecord entity, CancellationToken cancellationToken)
    {
        return _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public Task UpdateAsync(ApprovalOperationRecord entity, CancellationToken cancellationToken)
    {
        return _db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<ApprovalOperationRecord?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalOperationRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<ApprovalOperationRecord?> FindByIdempotencyKeyAsync(
        TenantId tenantId,
        long instanceId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalOperationRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.InstanceId == instanceId
                && x.IdempotencyKey == idempotencyKey)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalOperationRecord>> GetByInstanceIdAsync(
        TenantId tenantId,
        long instanceId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalOperationRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.InstanceId == instanceId)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .ToListAsync(cancellationToken);
    }
}

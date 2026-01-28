using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;

namespace Atlas.Application.Approval.Repositories;

/// <summary>
/// 审批操作记录仓储接口
/// </summary>
public interface IApprovalOperationRecordRepository
{
    /// <summary>
    /// 添加操作记录
    /// </summary>
    Task AddAsync(ApprovalOperationRecord entity, CancellationToken cancellationToken);

    /// <summary>
    /// 更新操作记录
    /// </summary>
    Task UpdateAsync(ApprovalOperationRecord entity, CancellationToken cancellationToken);

    /// <summary>
    /// 根据幂等键查找操作记录
    /// </summary>
    Task<ApprovalOperationRecord?> FindByIdempotencyKeyAsync(
        TenantId tenantId,
        long instanceId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// 根据实例ID查询操作记录列表
    /// </summary>
    Task<IReadOnlyList<ApprovalOperationRecord>> GetByInstanceIdAsync(
        TenantId tenantId,
        long instanceId,
        CancellationToken cancellationToken);
}

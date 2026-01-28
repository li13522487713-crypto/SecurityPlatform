using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Application.Approval.Repositories;

/// <summary>
/// 外部回调记录仓储接口
/// </summary>
public interface IApprovalExternalCallbackRecordRepository
{
    Task AddAsync(ApprovalExternalCallbackRecord entity, CancellationToken cancellationToken);

    Task UpdateAsync(ApprovalExternalCallbackRecord entity, CancellationToken cancellationToken);

    Task<ApprovalExternalCallbackRecord?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<ApprovalExternalCallbackRecord?> GetByIdempotencyKeyAsync(
        TenantId tenantId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalExternalCallbackRecord>> GetPendingRetriesAsync(
        TenantId tenantId,
        DateTimeOffset currentTime,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalExternalCallbackRecord>> GetByInstanceAsync(
        TenantId tenantId,
        long instanceId,
        CancellationToken cancellationToken);
}

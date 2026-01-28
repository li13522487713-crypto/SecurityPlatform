using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;

namespace Atlas.Application.Approval.Repositories;

/// <summary>
/// 抄送记录仓储接口
/// </summary>
public interface IApprovalCopyRecordRepository
{
    Task AddAsync(ApprovalCopyRecord entity, CancellationToken cancellationToken);

    Task AddRangeAsync(IEnumerable<ApprovalCopyRecord> entities, CancellationToken cancellationToken);

    Task UpdateAsync(ApprovalCopyRecord entity, CancellationToken cancellationToken);

    Task<ApprovalCopyRecord?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalCopyRecord>> GetByInstanceAsync(
        TenantId tenantId,
        long instanceId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalCopyRecord>> GetByRecipientAsync(
        TenantId tenantId,
        long recipientUserId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalCopyRecord>> GetByInstanceAndNodeAsync(
        TenantId tenantId,
        long instanceId,
        string nodeId,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<ApprovalCopyRecord> Items, int TotalCount)> GetPagedByRecipientAsync(
        TenantId tenantId,
        long recipientUserId,
        int pageIndex,
        int pageSize,
        bool? isRead = null,
        CancellationToken cancellationToken = default);
}

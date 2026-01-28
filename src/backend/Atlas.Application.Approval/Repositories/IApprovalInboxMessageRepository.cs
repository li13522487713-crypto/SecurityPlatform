using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;

namespace Atlas.Application.Approval.Repositories;

/// <summary>
/// 审批站内信仓储接口
/// </summary>
public interface IApprovalInboxMessageRepository
{
    Task AddAsync(ApprovalInboxMessage entity, CancellationToken cancellationToken);

    Task AddRangeAsync(IEnumerable<ApprovalInboxMessage> entities, CancellationToken cancellationToken);

    Task UpdateAsync(ApprovalInboxMessage entity, CancellationToken cancellationToken);

    Task<ApprovalInboxMessage?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<ApprovalInboxMessage> Items, int TotalCount)> GetPagedByRecipientAsync(
        TenantId tenantId,
        long recipientUserId,
        int pageIndex,
        int pageSize,
        bool? isRead = null,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(
        TenantId tenantId,
        long recipientUserId,
        CancellationToken cancellationToken);
}

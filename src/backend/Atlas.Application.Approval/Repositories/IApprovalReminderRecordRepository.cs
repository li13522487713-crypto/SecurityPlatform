using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;

namespace Atlas.Application.Approval.Repositories;

/// <summary>
/// 催办记录仓储接口
/// </summary>
public interface IApprovalReminderRecordRepository
{
    Task AddAsync(ApprovalReminderRecord entity, CancellationToken cancellationToken);

    Task<ApprovalReminderRecord?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalReminderRecord>> GetByInstanceAsync(
        TenantId tenantId,
        long instanceId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalReminderRecord>> GetByRecipientAsync(
        TenantId tenantId,
        long recipientUserId,
        CancellationToken cancellationToken);
}

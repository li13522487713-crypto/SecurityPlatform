using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Application.Approval.Repositories;

/// <summary>
/// 超时提醒记录仓储接口
/// </summary>
public interface IApprovalTimeoutReminderRepository
{
    Task AddAsync(ApprovalTimeoutReminder entity, CancellationToken cancellationToken);

    Task AddRangeAsync(IEnumerable<ApprovalTimeoutReminder> entities, CancellationToken cancellationToken);

    Task UpdateAsync(ApprovalTimeoutReminder entity, CancellationToken cancellationToken);

    Task<ApprovalTimeoutReminder?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalTimeoutReminder>> GetPendingRemindersAsync(
        TenantId tenantId,
        DateTimeOffset currentTime,
        CancellationToken cancellationToken);

    Task<ApprovalTimeoutReminder?> GetByInstanceAndTaskAsync(
        TenantId tenantId,
        long instanceId,
        long? taskId,
        string nodeId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalTimeoutReminder>> GetByInstanceAsync(
        TenantId tenantId,
        long instanceId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalTimeoutReminder>> GetByInstanceAndNodeAsync(
        TenantId tenantId,
        long instanceId,
        string nodeId,
        CancellationToken cancellationToken);
}

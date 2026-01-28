using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Application.Approval.Repositories;

/// <summary>
/// 审批通知模板仓储接口
/// </summary>
public interface IApprovalNotificationTemplateRepository
{
    Task AddAsync(ApprovalNotificationTemplate entity, CancellationToken cancellationToken);

    Task UpdateAsync(ApprovalNotificationTemplate entity, CancellationToken cancellationToken);

    Task<ApprovalNotificationTemplate?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<ApprovalNotificationTemplate?> GetByFlowAndEventAsync(
        TenantId tenantId,
        long flowDefinitionId,
        ApprovalNotificationEventType eventType,
        ApprovalNotificationChannel channel,
        CancellationToken cancellationToken);

    Task<ApprovalNotificationTemplate?> GetSystemTemplateAsync(
        TenantId tenantId,
        ApprovalNotificationEventType eventType,
        ApprovalNotificationChannel channel,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalNotificationTemplate>> GetByFlowAsync(
        TenantId tenantId,
        long flowDefinitionId,
        CancellationToken cancellationToken);
}

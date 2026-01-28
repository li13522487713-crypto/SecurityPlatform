using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Application.Approval.Repositories;

/// <summary>
/// 外部回调配置仓储接口
/// </summary>
public interface IApprovalExternalCallbackConfigRepository
{
    Task AddAsync(ApprovalExternalCallbackConfig entity, CancellationToken cancellationToken);

    Task UpdateAsync(ApprovalExternalCallbackConfig entity, CancellationToken cancellationToken);

    Task<ApprovalExternalCallbackConfig?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalExternalCallbackConfig>> GetByFlowAndEventAsync(
        TenantId tenantId,
        long flowDefinitionId,
        CallbackEventType eventType,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalExternalCallbackConfig>> GetSystemConfigsAsync(
        TenantId tenantId,
        CallbackEventType eventType,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalExternalCallbackConfig>> GetByFlowAsync(
        TenantId tenantId,
        long flowDefinitionId,
        CancellationToken cancellationToken);
}

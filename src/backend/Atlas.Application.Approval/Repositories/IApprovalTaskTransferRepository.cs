using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;

namespace Atlas.Application.Approval.Repositories;

/// <summary>
/// 审批任务转办记录仓储接口
/// </summary>
public interface IApprovalTaskTransferRepository
{
    Task AddAsync(ApprovalTaskTransfer entity, CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalTaskTransfer>> GetByTaskIdAsync(
        TenantId tenantId,
        long taskId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalTaskTransfer>> GetByInstanceAsync(
        TenantId tenantId,
        long instanceId,
        CancellationToken cancellationToken);
}

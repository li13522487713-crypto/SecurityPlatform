using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;

namespace Atlas.Application.Approval.Repositories;

/// <summary>
/// 审批任务加签/减签记录仓储接口
/// </summary>
public interface IApprovalTaskAssigneeChangeRepository
{
    Task AddAsync(ApprovalTaskAssigneeChange entity, CancellationToken cancellationToken);

    Task AddRangeAsync(IEnumerable<ApprovalTaskAssigneeChange> entities, CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalTaskAssigneeChange>> GetByInstanceAndNodeAsync(
        TenantId tenantId,
        long instanceId,
        string nodeId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalTaskAssigneeChange>> GetByInstanceAsync(
        TenantId tenantId,
        long instanceId,
        CancellationToken cancellationToken);
}

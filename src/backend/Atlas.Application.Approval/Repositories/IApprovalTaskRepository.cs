using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Application.Approval.Repositories;

/// <summary>
/// 审批任务仓储接口
/// </summary>
public interface IApprovalTaskRepository
{
    Task AddAsync(ApprovalTask entity, CancellationToken cancellationToken);

    Task AddRangeAsync(IEnumerable<ApprovalTask> entities, CancellationToken cancellationToken);

    Task UpdateAsync(ApprovalTask entity, CancellationToken cancellationToken);

    Task UpdateRangeAsync(IEnumerable<ApprovalTask> entities, CancellationToken cancellationToken);

    Task<ApprovalTask?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalTask>> QueryByIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<ApprovalTask> Items, int TotalCount)> GetPagedByInstanceAsync(
        TenantId tenantId,
        long instanceId,
        int pageIndex,
        int pageSize,
        ApprovalTaskStatus? status = null,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<ApprovalTask> Items, int TotalCount)> GetPagedByAssigneeAsync(
        TenantId tenantId,
        long userId,
        int pageIndex,
        int pageSize,
        ApprovalTaskStatus? status = null,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<ApprovalTask> Items, int TotalCount)> GetPagedPoolAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApprovalTask>> GetPendingByAssigneeUserAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalTask>> GetByInstanceAndNodeAsync(
        TenantId tenantId,
        long instanceId,
        string nodeId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalTask>> GetByInstanceAndNodesAsync(
        TenantId tenantId,
        long instanceId,
        IReadOnlyList<string> nodeIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalTask>> GetByInstanceAndStatusAsync(
        TenantId tenantId,
        long instanceId,
        ApprovalTaskStatus status,
        CancellationToken cancellationToken);

    Task<bool> ExistsByInstanceAndAssigneeAsync(
        TenantId tenantId,
        long instanceId,
        long assigneeUserId,
        CancellationToken cancellationToken);

    Task<int> CountByStatusAsync(
        TenantId tenantId,
        ApprovalTaskStatus status,
        DateTimeOffset? createdBefore,
        CancellationToken cancellationToken);
}

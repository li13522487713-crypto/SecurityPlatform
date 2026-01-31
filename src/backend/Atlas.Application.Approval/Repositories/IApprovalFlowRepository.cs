using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Application.Approval.Repositories;

/// <summary>
/// 审批流定义仓储接口
/// </summary>
public interface IApprovalFlowRepository
{
    Task AddAsync(ApprovalFlowDefinition entity, CancellationToken cancellationToken);

    Task UpdateAsync(ApprovalFlowDefinition entity, CancellationToken cancellationToken);

    Task<ApprovalFlowDefinition?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalFlowDefinition>> QueryByIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<ApprovalFlowDefinition> Items, int TotalCount)> GetPagedAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        ApprovalFlowStatus? status = null,
        string? keyword = null,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
}

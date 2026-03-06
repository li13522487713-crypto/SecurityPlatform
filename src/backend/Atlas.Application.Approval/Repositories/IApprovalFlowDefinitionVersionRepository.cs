using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;

namespace Atlas.Application.Approval.Repositories;

/// <summary>
/// 审批流定义版本快照仓储接口
/// </summary>
public interface IApprovalFlowDefinitionVersionRepository
{
    Task<ApprovalFlowDefinitionVersion?> GetByIdAsync(
        TenantId tenantId, long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApprovalFlowDefinitionVersion>> GetByDefinitionIdAsync(
        TenantId tenantId, long definitionId, CancellationToken cancellationToken = default);

    Task InsertAsync(ApprovalFlowDefinitionVersion entity, CancellationToken cancellationToken = default);

    Task DeleteByDefinitionIdAsync(
        TenantId tenantId, long definitionId, CancellationToken cancellationToken = default);
}

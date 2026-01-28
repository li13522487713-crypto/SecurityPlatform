using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;

namespace Atlas.Application.Approval.Repositories;

/// <summary>
/// 审批流程变量仓储接口
/// </summary>
public interface IApprovalProcessVariableRepository
{
    Task AddAsync(ApprovalProcessVariable entity, CancellationToken cancellationToken);

    Task UpdateAsync(ApprovalProcessVariable entity, CancellationToken cancellationToken);

    Task<ApprovalProcessVariable?> GetByInstanceAndNameAsync(
        TenantId tenantId,
        long instanceId,
        string variableName,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalProcessVariable>> GetByInstanceAsync(
        TenantId tenantId,
        long instanceId,
        CancellationToken cancellationToken);

    Task DeleteByInstanceAsync(
        TenantId tenantId,
        long instanceId,
        CancellationToken cancellationToken);
}

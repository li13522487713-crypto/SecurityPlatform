using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;

namespace Atlas.Application.Approval.Repositories;

/// <summary>
/// 审批流程变量仓储接口
/// TODO: 流程变量功能预留，待实现条件规则评估器时使用
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

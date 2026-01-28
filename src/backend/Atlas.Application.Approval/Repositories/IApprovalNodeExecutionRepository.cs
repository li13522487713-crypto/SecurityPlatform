using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Application.Approval.Repositories;

/// <summary>
/// 审批节点执行记录仓储接口
/// </summary>
public interface IApprovalNodeExecutionRepository
{
    Task AddAsync(ApprovalNodeExecution entity, CancellationToken cancellationToken);

    Task UpdateAsync(ApprovalNodeExecution entity, CancellationToken cancellationToken);

    Task<ApprovalNodeExecution?> GetByInstanceAndNodeAsync(
        TenantId tenantId,
        long instanceId,
        string nodeId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalNodeExecution>> GetByInstanceAsync(
        TenantId tenantId,
        long instanceId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalNodeExecution>> GetByInstanceAndStatusAsync(
        TenantId tenantId,
        long instanceId,
        ApprovalNodeExecutionStatus status,
        CancellationToken cancellationToken);
}

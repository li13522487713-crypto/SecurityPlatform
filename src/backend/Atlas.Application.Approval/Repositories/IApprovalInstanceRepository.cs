using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Application.Approval.Repositories;

/// <summary>
/// 审批流程实例仓储接口
/// </summary>
public interface IApprovalInstanceRepository
{
    Task AddAsync(ApprovalProcessInstance entity, CancellationToken cancellationToken);

    Task UpdateAsync(ApprovalProcessInstance entity, CancellationToken cancellationToken);

    Task<ApprovalProcessInstance?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalProcessInstance>> QueryByIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken);

    Task<ApprovalProcessInstance?> GetByBusinessKeyAsync(
        TenantId tenantId,
        string businessKey,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<ApprovalProcessInstance> Items, int TotalCount)> GetPagedByInitiatorAsync(
        TenantId tenantId,
        long initiatorUserId,
        int pageIndex,
        int pageSize,
        ApprovalInstanceStatus? status = null,
        IReadOnlyList<long>? restrictInitiatorUserIds = null,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<ApprovalProcessInstance> Items, int TotalCount)> GetPagedAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        long? definitionId = null,
        long? initiatorUserId = null,
        DateTimeOffset? startedFrom = null,
        DateTimeOffset? startedTo = null,
        string? businessKey = null,
        ApprovalInstanceStatus? status = null,
        IReadOnlyList<long>? restrictInitiatorUserIds = null,
        CancellationToken cancellationToken = default);
}

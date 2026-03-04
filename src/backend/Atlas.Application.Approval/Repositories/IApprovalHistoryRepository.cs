using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;

namespace Atlas.Application.Approval.Repositories;

/// <summary>
/// 审批历史事件仓储接口
/// </summary>
public interface IApprovalHistoryRepository
{
    Task AddAsync(ApprovalHistoryEvent entity, CancellationToken cancellationToken);

    Task AddRangeAsync(IEnumerable<ApprovalHistoryEvent> entities, CancellationToken cancellationToken);

    Task<(IReadOnlyList<ApprovalHistoryEvent> Items, int TotalCount)> GetPagedByInstanceAsync(
        TenantId tenantId,
        long instanceId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);
}

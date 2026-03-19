using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;

namespace Atlas.Application.Approval.Repositories;

/// <summary>
/// 审批通知重试记录仓储接口
/// </summary>
public interface IApprovalNotificationRetryRepository
{
    /// <summary>
    /// 添加重试记录
    /// </summary>
    Task AddAsync(ApprovalNotificationRetry entity, CancellationToken cancellationToken);

    /// <summary>
    /// 更新重试记录
    /// </summary>
    Task UpdateAsync(ApprovalNotificationRetry entity, CancellationToken cancellationToken);

    /// <summary>
    /// 获取待重试的记录（nextRetryAt <= now 且 status = Pending）
    /// </summary>
    /// <param name="batchSize">每次最多获取的记录数</param>
    Task<IReadOnlyList<ApprovalNotificationRetry>> GetPendingRetriesAsync(
        int batchSize,
        CancellationToken cancellationToken);
}

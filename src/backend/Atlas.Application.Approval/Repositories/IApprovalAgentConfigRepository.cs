using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;

namespace Atlas.Application.Approval.Repositories;

/// <summary>
/// 审批代理人配置仓储接口
/// </summary>
public interface IApprovalAgentConfigRepository
{
    Task AddAsync(ApprovalAgentConfig entity, CancellationToken cancellationToken);

    Task UpdateAsync(ApprovalAgentConfig entity, CancellationToken cancellationToken);

    Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken);

    Task<ApprovalAgentConfig?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);

    /// <summary>
    /// 获取指定用户当前生效的代理人配置
    /// </summary>
    Task<ApprovalAgentConfig?> GetActiveAgentAsync(
        TenantId tenantId,
        long principalUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// 批量获取多个用户当前生效的代理人配置（key 为委托人 userId）
    /// </summary>
    Task<IReadOnlyDictionary<long, ApprovalAgentConfig>> GetActiveAgentsByUserIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> principalUserIds,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// 获取指定用户的所有代理配置
    /// </summary>
    Task<IReadOnlyList<ApprovalAgentConfig>> GetByPrincipalUserIdAsync(
        TenantId tenantId,
        long principalUserId,
        CancellationToken cancellationToken);
}

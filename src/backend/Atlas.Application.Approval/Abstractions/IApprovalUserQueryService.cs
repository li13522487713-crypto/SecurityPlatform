using Atlas.Core.Tenancy;

namespace Atlas.Application.Approval.Abstractions;

/// <summary>
/// 审批模块用户查询服务接口（用于审批人策略查询）
/// 此接口抽象了用户/角色/部门查询能力，便于未来接入自有用户系统（任务12）
/// </summary>
public interface IApprovalUserQueryService
{
    /// <summary>
    /// 根据角色代码查询用户ID列表
    /// </summary>
    Task<IReadOnlyList<long>> GetUserIdsByRoleCodeAsync(
        TenantId tenantId,
        string roleCode,
        CancellationToken cancellationToken);

    /// <summary>
    /// 获取用户的直属领导用户ID
    /// </summary>
    Task<long?> GetDirectLeaderUserIdAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// 层层审批：向上逐级查找审批人（直到找到有审批权限的用户或到达顶层）
    /// </summary>
    /// <param name="tenantId">租户ID</param>
    /// <param name="startUserId">起始用户ID</param>
    /// <param name="maxLevels">最大查找层级（防止无限循环，默认10层）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>审批人用户ID列表（按层级从低到高）</returns>
    Task<IReadOnlyList<long>> GetLoopApproversAsync(
        TenantId tenantId,
        long startUserId,
        int maxLevels = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定层级：向上查找指定层级的审批人
    /// </summary>
    /// <param name="tenantId">租户ID</param>
    /// <param name="startUserId">起始用户ID</param>
    /// <param name="targetLevel">目标层级（1表示直属领导，2表示上级的上级，以此类推）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>审批人用户ID，如果层级不足则返回null</returns>
    Task<long?> GetLevelApproverAsync(
        TenantId tenantId,
        long startUserId,
        int targetLevel,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取用户的HRBP（人力资源业务伙伴）用户ID
    /// </summary>
    Task<long?> GetHrbpUserIdAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// 根据用户ID列表验证用户是否存在且有效
    /// </summary>
    Task<IReadOnlyList<long>> ValidateUserIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> userIds,
        CancellationToken cancellationToken);
}

using Atlas.Core.Tenancy;

namespace Atlas.Application.Approval.Abstractions;

/// <summary>
/// 审批模块用户查询服务接口（用于审批人策略查询）
/// 
/// 此接口抽象了审批流程所需的最小用户/角色/部门查询能力，便于未来接入自有用户系统。
/// 
/// **替换实现方式：**
/// 1. 实现此接口，提供自定义的用户/角色/部门查询逻辑
/// 2. 在 DI 容器中注册自定义实现，替换默认的 `ApprovalUserQueryService`
/// 3. 例如：services.AddScoped&lt;IApprovalUserQueryService, CustomUserQueryService&gt;();
/// 
/// **最小能力要求：**
/// - 按角色代码查询用户ID列表（支持 Role 审批人策略）
/// - 查询用户的直属领导（支持 DirectLeader 审批人策略）
/// - 向上逐级查找审批人（支持 Loop 层层审批策略）
/// - 查询指定层级的审批人（支持 Level 指定层级策略）
/// - 查询用户的HRBP（支持 HRBP 审批人策略）
/// - 验证用户ID有效性（用于所有审批人策略的最终校验）
/// 
/// **注意事项：**
/// - 所有方法必须支持多租户隔离（通过 TenantId 参数）
/// - 方法应异步执行，避免阻塞
/// - 如果查询不到结果，返回空列表或 null（不要抛出异常）
/// - 实现应保证性能，避免 N+1 查询问题
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
    /// 根据角色代码列表查询用户ID列表（聚合去重）
    /// </summary>
    Task<IReadOnlyList<long>> GetUserIdsByRoleCodesAsync(
        TenantId tenantId,
        IReadOnlyList<string> roleCodes,
        CancellationToken cancellationToken);

    /// <summary>
    /// 获取用户的直属领导用户ID
    /// </summary>
    /// <param name="appId">应用 ID（非 null 时走应用级组织查询，null 走平台级）</param>
    Task<long?> GetDirectLeaderUserIdAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken,
        long? appId = null);

    /// <summary>
    /// 层层审批：向上逐级查找审批人（直到找到有审批权限的用户或到达顶层）
    /// </summary>
    /// <param name="tenantId">租户ID</param>
    /// <param name="startUserId">起始用户ID</param>
    /// <param name="maxLevels">最大查找层级（防止无限循环，默认10层）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="appId">应用 ID（非 null 时走应用级组织查询，null 走平台级）</param>
    /// <returns>审批人用户ID列表（按层级从低到高）</returns>
    Task<IReadOnlyList<long>> GetLoopApproversAsync(
        TenantId tenantId,
        long startUserId,
        int maxLevels = 10,
        CancellationToken cancellationToken = default,
        long? appId = null);

    /// <summary>
    /// 指定层级：向上查找指定层级的审批人
    /// </summary>
    /// <param name="tenantId">租户ID</param>
    /// <param name="startUserId">起始用户ID</param>
    /// <param name="targetLevel">目标层级（1表示直属领导，2表示上级的上级，以此类推）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="appId">应用 ID（非 null 时走应用级组织查询，null 走平台级）</param>
    /// <returns>审批人用户ID，如果层级不足则返回null</returns>
    Task<long?> GetLevelApproverAsync(
        TenantId tenantId,
        long startUserId,
        int targetLevel,
        CancellationToken cancellationToken = default,
        long? appId = null);

    /// <summary>
    /// 获取用户所在部门的负责人用户ID
    /// </summary>
    /// <param name="appId">应用 ID（非 null 时走应用级组织查询，null 走平台级）</param>
    Task<long?> GetDepartmentHeadUserIdAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken,
        long? appId = null);

    /// <summary>
    /// 获取用户的HRBP（人力资源业务伙伴）用户ID
    /// </summary>
    /// <param name="appId">应用 ID（非 null 时走应用级组织查询，null 走平台级）</param>
    Task<long?> GetHrbpUserIdAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken,
        long? appId = null);

    /// <summary>
    /// 根据用户ID列表验证用户是否存在且有效
    /// </summary>
    Task<IReadOnlyList<long>> ValidateUserIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> userIds,
        CancellationToken cancellationToken);
}

using Atlas.Core.Tenancy;

namespace Atlas.Application.Approval.Abstractions;

/// <summary>
/// 审批模块角色查询服务接口契约
/// 
/// 此接口定义了审批流程所需的最小角色查询能力，允许开发者替换实现以接入自有角色系统。
/// 
/// **替换实现方式：**
/// 1. 实现此接口，提供自定义的角色查询逻辑
/// 2. 在 DI 容器中注册自定义实现，替换默认实现
/// 3. 例如：services.AddScoped&lt;IApprovalRoleService, CustomRoleService&gt;();
/// 
/// **最小能力要求：**
/// - 按角色代码查询用户ID列表（支持 Role 审批人策略）
/// 
/// **注意事项：**
/// - 所有方法必须支持多租户隔离（通过 TenantId 参数）
/// - 方法应异步执行，避免阻塞
/// - 如果查询不到结果，返回空列表（不要抛出异常）
/// - 实现应保证性能，避免 N+1 查询问题
/// </summary>
public interface IApprovalRoleService
{
    /// <summary>
    /// 根据角色代码查询用户ID列表
    /// </summary>
    /// <param name="tenantId">租户ID</param>
    /// <param name="roleCode">角色代码</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>该角色下的所有用户ID列表</returns>
    Task<IReadOnlyList<long>> GetUserIdsByRoleCodeAsync(
        TenantId tenantId,
        string roleCode,
        CancellationToken cancellationToken);
}

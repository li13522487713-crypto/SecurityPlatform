using Atlas.Core.Tenancy;

namespace Atlas.Application.Approval.Abstractions;

/// <summary>
/// 审批模块部门查询服务接口契约
/// 
/// 此接口定义了审批流程所需的最小部门查询能力，允许开发者替换实现以接入自有部门系统。
/// 
/// **替换实现方式：**
/// 1. 实现此接口，提供自定义的部门查询逻辑
/// 2. 在 DI 容器中注册自定义实现，替换默认实现
/// 3. 例如：services.AddScoped&lt;IApprovalDepartmentService, CustomDepartmentService&gt;();
/// 
/// **最小能力要求：**
/// - 查询部门负责人用户ID（支持 DepartmentLeader 审批人策略）
/// 
/// **注意事项：**
/// - 所有方法必须支持多租户隔离（通过 TenantId 参数）
/// - 方法应异步执行，避免阻塞
/// - 如果查询不到结果，返回 null（不要抛出异常）
/// - 实现应保证性能，避免 N+1 查询问题
/// </summary>
public interface IApprovalDepartmentService
{
    /// <summary>
    /// 获取部门负责人用户ID
    /// </summary>
    /// <param name="tenantId">租户ID</param>
    /// <param name="departmentId">部门ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>部门负责人用户ID，如果不存在则返回null</returns>
    Task<long?> GetLeaderUserIdAsync(
        TenantId tenantId,
        long departmentId,
        CancellationToken cancellationToken);
}

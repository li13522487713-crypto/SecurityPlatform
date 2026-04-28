using System.Threading;
using System.Threading.Tasks;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Audit.Abstractions;

/// <summary>
/// 治理 M-G04-C2（S8）：资源可见性解析器。
///
/// 给定 (tenant, user) 与一组待过滤的 (resourceType, resourceId)，返回 user 可见的子集。
/// 服务层（AuditQueryService / RuntimeMessageLogService）调用本接口收口列表查询的结果，
/// 默认收缩到 owner + collaborator + 工作空间管理员；scope=all 由调用方在控制器层显式开启（管理员场景）。
/// </summary>
public interface IResourceVisibilityResolver
{
    /// <summary>
    /// 返回用户可见的 (resourceType, resourceId) 子集。
    /// 实现允许走 cache（按 IPermissionDecisionService.InvalidateResourceAsync 失效）。
    /// </summary>
    Task<IReadOnlyCollection<(string ResourceType, string ResourceId)>> FilterVisibleAsync(
        TenantId tenantId,
        long userId,
        bool isPlatformAdmin,
        IReadOnlyCollection<(string ResourceType, string ResourceId)> candidates,
        CancellationToken cancellationToken);
}

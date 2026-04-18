using Atlas.Core.Tenancy;

namespace Atlas.Application.Identity.Abstractions;

/// <summary>
/// 统一权限决策服务（PDP）。
/// 负责集中执行权限判定，并提供缓存失效能力。
/// </summary>
public interface IPermissionDecisionService
{
    Task<bool> HasPermissionAsync(
        TenantId tenantId,
        long userId,
        string permissionCode,
        CancellationToken cancellationToken);

    Task<bool> IsSystemAdminAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken);

    Task InvalidateUserAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken = default);

    Task InvalidateRoleAsync(
        TenantId tenantId,
        long roleId,
        CancellationToken cancellationToken = default);

    Task InvalidateTenantAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 治理 M-G03-C1：当资源 ACL 变更时（owner 更换、协作者增删、角色权限改），
    /// 失效该资源相关的 PDP 缓存。一般在 ResourceAccessGuard 内部按 tag 处理；
    /// 这里暴露顶层 API 是为 Service 层在更新 WorkspaceResourcePermission 后显式调用。
    /// </summary>
    Task InvalidateResourceAsync(
        TenantId tenantId,
        string resourceType,
        long resourceId,
        CancellationToken cancellationToken = default);
}

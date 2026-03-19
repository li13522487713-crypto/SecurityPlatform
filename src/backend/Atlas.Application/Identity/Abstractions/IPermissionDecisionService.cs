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
}

using Atlas.Application.Abstractions;
using Atlas.Application.Identity;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Caching;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 基于 HybridCache 的统一权限决策服务。
/// </summary>
public sealed class PermissionDecisionService : IPermissionDecisionService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly IAtlasHybridCache _cache;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IRbacResolver _rbacResolver;

    public PermissionDecisionService(
        IAtlasHybridCache cache,
        IUserAccountRepository userAccountRepository,
        IUserRoleRepository userRoleRepository,
        IRbacResolver rbacResolver)
    {
        _cache = cache;
        _userAccountRepository = userAccountRepository;
        _userRoleRepository = userRoleRepository;
        _rbacResolver = rbacResolver;
    }

    public async Task<bool> HasPermissionAsync(
        TenantId tenantId,
        long userId,
        string permissionCode,
        CancellationToken cancellationToken)
    {
        var cacheEntry = await GetOrCreateAsync(tenantId, userId, cancellationToken);
        if (!cacheEntry.IsActive)
        {
            return false;
        }

        if (cacheEntry.IsPlatformAdmin)
        {
            return true;
        }

        if (cacheEntry.PermissionCodes.Contains(PermissionCodes.SystemAdmin, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(permissionCode, PermissionCodes.WorkflowView, StringComparison.OrdinalIgnoreCase)
            && cacheEntry.PermissionCodes.Contains(PermissionCodes.WorkflowDesign, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return cacheEntry.PermissionCodes.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<bool> IsSystemAdminAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        var cacheEntry = await GetOrCreateAsync(tenantId, userId, cancellationToken);
        if (!cacheEntry.IsActive)
        {
            return false;
        }

        if (cacheEntry.IsPlatformAdmin)
        {
            return true;
        }

        if (cacheEntry.PermissionCodes.Contains(PermissionCodes.SystemAdmin, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return cacheEntry.RoleCodes.Contains("Admin", StringComparer.OrdinalIgnoreCase)
            || cacheEntry.RoleCodes.Contains("SuperAdmin", StringComparer.OrdinalIgnoreCase);
    }

    public Task InvalidateUserAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        return _cache.RemoveByTagAsync(AtlasCacheTags.IdentityUser(tenantId, userId), cancellationToken).AsTask();
    }

    public async Task InvalidateRoleAsync(
        TenantId tenantId,
        long roleId,
        CancellationToken cancellationToken = default)
    {
        var userIds = await _userRoleRepository.QueryUserIdsByRoleIdAsync(tenantId, roleId, cancellationToken);
        if (userIds.Count == 0)
        {
            return;
        }

        foreach (var userId in userIds.Distinct())
        {
            await _cache.RemoveByTagAsync(AtlasCacheTags.IdentityUser(tenantId, userId), cancellationToken);
        }
    }

    public Task InvalidateTenantAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        return _cache.RemoveByTagAsync(AtlasCacheTags.IdentityTenant(tenantId), cancellationToken).AsTask();
    }

    private async Task<PermissionDecisionCacheEntry> GetOrCreateAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        var cacheKey = AtlasCacheKeys.Identity.PermissionDecision(tenantId, userId);
        var result = await _cache.GetOrCreateAsync(
            cacheKey,
            async ct =>
            {
                var account = await _userAccountRepository.FindByIdAsync(tenantId, userId, ct);
                if (account is null)
                {
                    return PermissionDecisionCacheEntry.Empty;
                }

                var (roleCodes, permissionCodes) = await _rbacResolver.GetRolesAndPermissionsAsync(account, tenantId, ct);
                return new PermissionDecisionCacheEntry(
                    account.IsActive,
                    account.IsPlatformAdmin,
                    roleCodes,
                    permissionCodes);
            },
            CacheTtl,
            [AtlasCacheTags.IdentityUser(tenantId, userId), AtlasCacheTags.IdentityTenant(tenantId)],
            cancellationToken: cancellationToken);

        return result ?? PermissionDecisionCacheEntry.Empty;
    }

    private sealed record PermissionDecisionCacheEntry(
        bool IsActive,
        bool IsPlatformAdmin,
        IReadOnlyList<string> RoleCodes,
        IReadOnlyList<string> PermissionCodes)
    {
        public static readonly PermissionDecisionCacheEntry Empty = new(
            false,
            false,
            Array.Empty<string>(),
            Array.Empty<string>());
    }
}

using Atlas.Application.Abstractions;
using Atlas.Application.Identity;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Identity;
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
    private readonly IAppContextAccessor _appContextAccessor;

    public PermissionDecisionService(
        IAtlasHybridCache cache,
        IUserAccountRepository userAccountRepository,
        IUserRoleRepository userRoleRepository,
        IRbacResolver rbacResolver)
        : this(
            cache,
            userAccountRepository,
            userRoleRepository,
            rbacResolver,
            NullAppContextAccessor.Instance)
    {
    }

    public PermissionDecisionService(
        IAtlasHybridCache cache,
        IUserAccountRepository userAccountRepository,
        IUserRoleRepository userRoleRepository,
        IRbacResolver rbacResolver,
        IAppContextAccessor appContextAccessor)
    {
        _cache = cache;
        _userAccountRepository = userAccountRepository;
        _userRoleRepository = userRoleRepository;
        _rbacResolver = rbacResolver;
        _appContextAccessor = appContextAccessor;
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

        if (cacheEntry.IsAppScoped && cacheEntry.RoleCodes.Contains("AppAdmin", StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        if (cacheEntry.PermissionCodes.Contains("*:*:*", StringComparer.OrdinalIgnoreCase))
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

        if (cacheEntry.IsAppScoped)
        {
            return cacheEntry.PermissionCodes.Contains(PermissionCodes.AppAdmin, StringComparer.OrdinalIgnoreCase)
                || cacheEntry.RoleCodes.Contains("AppAdmin", StringComparer.OrdinalIgnoreCase);
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

    public Task InvalidateResourceAsync(
        TenantId tenantId,
        string resourceType,
        long resourceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(resourceType) || resourceId <= 0)
        {
            return Task.CompletedTask;
        }
        return _cache.RemoveByTagAsync(AtlasCacheTags.Resource(tenantId, resourceType, resourceId), cancellationToken).AsTask();
    }

    private async Task<PermissionDecisionCacheEntry> GetOrCreateAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        var appId = ResolveAppId();
        var cacheKey = AtlasCacheKeys.Identity.PermissionDecision(tenantId, userId, appId);
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
                    appId.HasValue,
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
        bool IsAppScoped,
        IReadOnlyList<string> RoleCodes,
        IReadOnlyList<string> PermissionCodes)
    {
        public static readonly PermissionDecisionCacheEntry Empty = new(
            false,
            false,
            false,
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private long? ResolveAppId()
    {
        var appId = _appContextAccessor.ResolveAppId();
        return appId is > 0 ? appId : null;
    }

    private sealed class NullAppContextAccessor : IAppContextAccessor
    {
        public static readonly NullAppContextAccessor Instance = new();

        public IAppContext GetCurrent()
        {
            return new AppContextSnapshot(
                TenantId.Empty,
                string.Empty,
                null,
                new ClientContext(ClientType.WebH5, ClientPlatform.Web, ClientChannel.Browser, ClientAgent.Other),
                null);
        }

        public string GetAppId() => string.Empty;

        public IDisposable BeginScope(IAppContext context) => NoopDisposable.Instance;
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}

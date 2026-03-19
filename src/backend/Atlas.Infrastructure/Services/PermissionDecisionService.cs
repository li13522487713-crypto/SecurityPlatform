using Atlas.Application.Abstractions;
using Atlas.Application.Identity;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Tenancy;
using Microsoft.Extensions.Caching.Memory;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 基于 IMemoryCache 的统一权限决策服务。
/// </summary>
public sealed class PermissionDecisionService : IPermissionDecisionService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly IMemoryCache _cache;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IRbacResolver _rbacResolver;

    public PermissionDecisionService(
        IMemoryCache cache,
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
        cancellationToken.ThrowIfCancellationRequested();
        _cache.Remove(UserCacheKey(tenantId, userId));
        return Task.CompletedTask;
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
            _cache.Remove(UserCacheKey(tenantId, userId));
        }
    }

    public Task InvalidateTenantAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tenantKey = TenantIndexKey(tenantId);
        if (_cache.TryGetValue(tenantKey, out HashSet<long>? users) && users is not null)
        {
            lock (users)
            {
                foreach (var userId in users)
                {
                    _cache.Remove(UserCacheKey(tenantId, userId));
                }
            }
        }

        _cache.Remove(tenantKey);
        return Task.CompletedTask;
    }

    private async Task<PermissionDecisionCacheEntry> GetOrCreateAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        var cacheKey = UserCacheKey(tenantId, userId);
        if (_cache.TryGetValue(cacheKey, out PermissionDecisionCacheEntry? cached) && cached is not null)
        {
            return cached;
        }

        var account = await _userAccountRepository.FindByIdAsync(tenantId, userId, cancellationToken);
        if (account is null)
        {
            var missing = PermissionDecisionCacheEntry.Empty;
            SetCacheEntry(tenantId, userId, missing);
            return missing;
        }

        var roleCodes = await _rbacResolver.GetRoleCodesAsync(account, tenantId, cancellationToken);
        var permissionCodes = await _rbacResolver.GetPermissionCodesAsync(tenantId, userId, cancellationToken);
        var cacheEntry = new PermissionDecisionCacheEntry(
            account.IsActive,
            account.IsPlatformAdmin,
            roleCodes,
            permissionCodes);
        SetCacheEntry(tenantId, userId, cacheEntry);
        return cacheEntry;
    }

    private void SetCacheEntry(
        TenantId tenantId,
        long userId,
        PermissionDecisionCacheEntry entry)
    {
        var cacheKey = UserCacheKey(tenantId, userId);
        _cache.Set(cacheKey, entry, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheTtl,
            PostEvictionCallbacks =
            {
                new PostEvictionCallbackRegistration
                {
                    EvictionCallback = (_, _, _, _) => RemoveUserFromTenantIndex(tenantId, userId)
                }
            }
        });

        AddUserToTenantIndex(tenantId, userId);
    }

    private void AddUserToTenantIndex(TenantId tenantId, long userId)
    {
        var tenantKey = TenantIndexKey(tenantId);
        var users = _cache.GetOrCreate(tenantKey, _ => new HashSet<long>())!;
        lock (users)
        {
            users.Add(userId);
        }
    }

    private void RemoveUserFromTenantIndex(TenantId tenantId, long userId)
    {
        var tenantKey = TenantIndexKey(tenantId);
        if (_cache.TryGetValue(tenantKey, out HashSet<long>? users) && users is not null)
        {
            lock (users)
            {
                users.Remove(userId);
            }
        }
    }

    private static string UserCacheKey(TenantId tenantId, long userId)
        => $"perm:u:{tenantId.Value:N}:{userId}";

    private static string TenantIndexKey(TenantId tenantId)
        => $"perm:t:{tenantId.Value:N}";

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

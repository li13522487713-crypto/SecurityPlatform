using Atlas.Application.Abstractions;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Identity;
using Atlas.Application.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Caching;
using Atlas.Application.Identity;

namespace Atlas.Infrastructure.Services;

public sealed class AuthProfileService : IAuthProfileService
{
    private readonly IUserAccountRepository _userRepository;
    private readonly IRbacResolver _rbacResolver;
    private readonly IAtlasHybridCache _cache;
    private readonly IAppContextAccessor _appContextAccessor;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public AuthProfileService(
        IUserAccountRepository userRepository,
        IRbacResolver rbacResolver,
        IAppContextAccessor appContextAccessor,
        IAtlasHybridCache cache)
    {
        _userRepository = userRepository;
        _rbacResolver = rbacResolver;
        _appContextAccessor = appContextAccessor;
        _cache = cache;
    }

    public async Task<AuthProfileResult?> GetProfileAsync(
        long userId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var appId = ResolveAppId();
        var cacheKey = AtlasCacheKeys.Identity.AuthProfile(tenantId, userId, appId);
        var cached = await _cache.TryGetAsync<AuthProfileResult>(cacheKey, cancellationToken: cancellationToken);
        if (cached.Found)
        {
            return cached.Value;
        }

        var account = await _userRepository.FindByIdAsync(tenantId, userId, cancellationToken);
        if (account is null)
        {
            return null;
        }

        var (roleCodes, permissionCodes) = await _rbacResolver.GetRolesAndPermissionsAsync(account, tenantId, cancellationToken);
        var effectivePermissionCodes = permissionCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (account.IsPlatformAdmin)
        {
            effectivePermissionCodes.Add(PermissionCodes.SystemAdmin);
            effectivePermissionCodes.Add(PermissionCodes.AppAdmin);
            effectivePermissionCodes.Add("*:*:*");
        }

        if (appId.HasValue && roleCodes.Contains("AppAdmin", StringComparer.OrdinalIgnoreCase))
        {
            effectivePermissionCodes.Add(PermissionCodes.AppAdmin);
            effectivePermissionCodes.Add("*:*:*");
        }

        var result = new AuthProfileResult(
            account.Id.ToString(),
            account.Username,
            account.DisplayName,
            tenantId.Value.ToString("D"),
            roleCodes,
            effectivePermissionCodes.ToArray(),
            account.IsPlatformAdmin,
            null);

        await _cache.SetAsync(
            cacheKey,
            result,
            CacheTtl,
            [AtlasCacheTags.IdentityUser(tenantId, userId), AtlasCacheTags.IdentityTenant(tenantId)],
            cancellationToken: cancellationToken);
        return result;
    }

    private long? ResolveAppId()
    {
        var appId = _appContextAccessor.ResolveAppId();
        return appId is > 0 ? appId : null;
    }
}

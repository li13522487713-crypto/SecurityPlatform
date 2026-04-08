using Atlas.Application.Abstractions;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Repositories;
using Atlas.Application.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Caching;

namespace Atlas.Infrastructure.Services;

public sealed class AuthProfileService : IAuthProfileService
{
    private readonly IUserAccountRepository _userRepository;
    private readonly IRbacResolver _rbacResolver;
    private readonly IAtlasHybridCache _cache;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public AuthProfileService(
        IUserAccountRepository userRepository,
        IRbacResolver rbacResolver,
        IAtlasHybridCache cache)
    {
        _userRepository = userRepository;
        _rbacResolver = rbacResolver;
        _cache = cache;
    }

    public async Task<AuthProfileResult?> GetProfileAsync(
        long userId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var cacheKey = AtlasCacheKeys.Identity.AuthProfile(tenantId, userId);
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

        var result = new AuthProfileResult(
            account.Id.ToString(),
            account.Username,
            account.DisplayName,
            tenantId.Value.ToString("D"),
            roleCodes,
            permissionCodes,
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
}

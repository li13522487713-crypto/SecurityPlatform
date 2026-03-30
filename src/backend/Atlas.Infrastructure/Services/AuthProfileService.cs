using Atlas.Application.Abstractions;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Repositories;
using Atlas.Application.Models;
using Atlas.Core.Tenancy;
using Microsoft.Extensions.Caching.Memory;

namespace Atlas.Infrastructure.Services;

public sealed class AuthProfileService : IAuthProfileService
{
    private readonly IUserAccountRepository _userRepository;
    private readonly IRbacResolver _rbacResolver;
    private readonly IMemoryCache _cache;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public AuthProfileService(
        IUserAccountRepository userRepository,
        IRbacResolver rbacResolver,
        IMemoryCache cache)
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
        var cacheKey = $"auth_profile_{tenantId.Value:D}_{userId}";
        if (_cache.TryGetValue(cacheKey, out AuthProfileResult? cached))
        {
            return cached;
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

        _cache.Set(cacheKey, result, CacheTtl);
        return result;
    }
}

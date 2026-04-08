using Atlas.Application.Identity.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Caching;

namespace Atlas.Infrastructure.Security;

/// <summary>
/// 基于 HybridCache 的 JWT 认证缓存实现。
///
/// 缓存设计原则：
/// - TTL = 60 秒（绝对过期），短于 JWT 有效期，确保安全边界。
/// - 按 session 失效（退出登录）和按 user 失效（禁用/改密）两条路径均可精确失效。
/// - 不缓存任何敏感字段（密码哈希、MFA Key等），仅缓存验证所需最小状态。
/// </summary>
public sealed class HybridAuthCacheService : IAuthCacheService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private readonly IAtlasHybridCache _cache;

    public HybridAuthCacheService(IAtlasHybridCache cache)
    {
        _cache = cache;
    }

    public async Task<AuthValidationCacheEntry?> GetAsync(TenantId tenantId, long userId, long sessionId)
    {
        var result = await _cache.TryGetAsync<AuthValidationCacheEntry>(
            AtlasCacheKeys.Identity.AuthSession(tenantId, sessionId));
        return result.Found ? result.Value : null;
    }

    public Task SetAsync(TenantId tenantId, long userId, long sessionId, AuthValidationCacheEntry entry)
    {
        return _cache.SetAsync(
            AtlasCacheKeys.Identity.AuthSession(tenantId, sessionId),
            entry,
            Ttl,
            [AtlasCacheTags.IdentityUser(tenantId, userId)]).AsTask();
    }

    public Task InvalidateSessionAsync(TenantId tenantId, long sessionId)
    {
        return _cache.RemoveAsync(AtlasCacheKeys.Identity.AuthSession(tenantId, sessionId)).AsTask();
    }

    public Task InvalidateUserAsync(TenantId tenantId, long userId)
    {
        return _cache.RemoveByTagAsync(AtlasCacheTags.IdentityUser(tenantId, userId)).AsTask();
    }
}

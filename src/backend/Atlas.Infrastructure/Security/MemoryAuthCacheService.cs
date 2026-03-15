using Atlas.Application.Identity.Abstractions;
using Atlas.Core.Tenancy;
using Microsoft.Extensions.Caching.Memory;

namespace Atlas.Infrastructure.Security;

/// <summary>
/// 基于 IMemoryCache 的 JWT 认证缓存实现。
///
/// 缓存设计原则：
/// - TTL = 60 秒（绝对过期），短于 JWT 有效期，确保安全边界。
/// - 按 session 失效（退出登录）和按 user 失效（禁用/改密）两条路径均有索引支持。
/// - 不缓存任何敏感字段（密码哈希、MFA Key等），仅缓存验证所需最小状态。
/// </summary>
public sealed class MemoryAuthCacheService : IAuthCacheService
{
    // 主缓存键：session 维度（一个 session 对应一个条目）
    private static string SessionKey(TenantId tenantId, long sessionId)
        => $"auth:s:{tenantId.Value:N}:{sessionId}";

    // 用户维度索引键：存储该用户当前活跃的 sessionId 集合，用于按 user 批量失效
    private static string UserIndexKey(TenantId tenantId, long userId)
        => $"auth:u:{tenantId.Value:N}:{userId}";

    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private readonly IMemoryCache _cache;

    public MemoryAuthCacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public AuthValidationCacheEntry? Get(TenantId tenantId, long userId, long sessionId)
    {
        var key = SessionKey(tenantId, sessionId);
        return _cache.TryGetValue(key, out AuthValidationCacheEntry? entry) ? entry : null;
    }

    public void Set(TenantId tenantId, long userId, long sessionId, AuthValidationCacheEntry entry)
    {
        var sessionKey = SessionKey(tenantId, sessionId);
        _cache.Set(sessionKey, entry, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = Ttl,
            // 条目过期时从用户索引中移除，避免索引无限增长
            PostEvictionCallbacks =
            {
                new PostEvictionCallbackRegistration
                {
                    EvictionCallback = (_, _, _, _) => RemoveSessionFromUserIndex(tenantId, userId, sessionId),
                    State = null
                }
            }
        });

        // 维护用户→session 索引（不设 TTL，条目会随 session 过期被清理）
        AddSessionToUserIndex(tenantId, userId, sessionId);
    }

    public void InvalidateSession(TenantId tenantId, long sessionId)
    {
        _cache.Remove(SessionKey(tenantId, sessionId));
        // 注意：PostEvictionCallback 会自动从用户索引移除，此处无需额外清理
    }

    public void InvalidateUser(TenantId tenantId, long userId)
    {
        var indexKey = UserIndexKey(tenantId, userId);
        if (_cache.TryGetValue(indexKey, out HashSet<long>? sessionIds) && sessionIds is not null)
        {
            foreach (var sid in sessionIds)
            {
                _cache.Remove(SessionKey(tenantId, sid));
            }
        }

        _cache.Remove(indexKey);
    }

    private void AddSessionToUserIndex(TenantId tenantId, long userId, long sessionId)
    {
        var indexKey = UserIndexKey(tenantId, userId);
        var sessions = _cache.GetOrCreate(indexKey, _ => new HashSet<long>())!;
        lock (sessions)
        {
            sessions.Add(sessionId);
        }
    }

    private void RemoveSessionFromUserIndex(TenantId tenantId, long userId, long sessionId)
    {
        var indexKey = UserIndexKey(tenantId, userId);
        if (_cache.TryGetValue(indexKey, out HashSet<long>? sessions) && sessions is not null)
        {
            lock (sessions)
            {
                sessions.Remove(sessionId);
            }
        }
    }
}

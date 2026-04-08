using Atlas.Core.Tenancy;

namespace Atlas.Application.Identity.Abstractions;

/// <summary>
/// JWT 认证缓存服务：缓存 user + session 验证结果，减少每次请求的 DB 查询。
/// 缓存 TTL 设为 60 秒，安全写操作（禁用用户、撤销会话）必须主动调用失效接口。
/// </summary>
public interface IAuthCacheService
{
    /// <summary>
    /// 获取缓存的认证验证结果。若未命中，返回 null。
    /// </summary>
    Task<AuthValidationCacheEntry?> GetAsync(TenantId tenantId, long userId, long sessionId);

    /// <summary>
    /// 将认证验证结果写入缓存，TTL 由实现方决定（≤ 60 秒）。
    /// </summary>
    Task SetAsync(TenantId tenantId, long userId, long sessionId, AuthValidationCacheEntry entry);

    /// <summary>
    /// 使指定 session 的缓存条目失效。
    /// 用户退出登录、会话被撤销时必须调用。
    /// </summary>
    Task InvalidateSessionAsync(TenantId tenantId, long sessionId);

    /// <summary>
    /// 使指定用户的所有缓存条目失效。
    /// 用户被禁用、密码变更、批量撤销会话时必须调用。
    /// </summary>
    Task InvalidateUserAsync(TenantId tenantId, long userId);
}

/// <summary>
/// 认证缓存条目，仅保留验证所需的最小字段，不缓存敏感数据。
/// </summary>
public sealed record AuthValidationCacheEntry(
    bool IsUserActive,
    long UserId,
    long SessionId,
    DateTimeOffset SessionExpiresAt,
    bool IsSessionRevoked);

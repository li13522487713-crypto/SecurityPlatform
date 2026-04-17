using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

/// <summary>
/// 应用草稿稿锁服务（M04 S04-2）。
/// 多设备并发编辑时仅允许 1 个持有者；其他设备得到警告并可"强制夺锁"。
///
/// 锁基于 SQLite + 心跳：
/// - 持有者每 30s 调用一次 RenewAsync，否则视为过期。
/// - 强制夺锁需高权限（M14 与权限矩阵联动），并写审计。
/// </summary>
public interface IAppDraftLockService
{
    /// <summary>
    /// 尝试获取锁；若已被他人持有且未过期，返回 false 与持有者信息。
    /// </summary>
    Task<AppDraftLockResult> TryAcquireAsync(TenantId tenantId, long appId, long userId, string sessionId, CancellationToken cancellationToken);

    /// <summary>续约心跳；返回 false 表示锁已被他人夺取。</summary>
    Task<bool> RenewAsync(TenantId tenantId, long appId, string sessionId, CancellationToken cancellationToken);

    /// <summary>主动释放锁。</summary>
    Task ReleaseAsync(TenantId tenantId, long appId, string sessionId, CancellationToken cancellationToken);

    /// <summary>强制夺锁（高权限场景），写审计。</summary>
    Task<AppDraftLockResult> ForceTakeoverAsync(TenantId tenantId, long appId, long newOwnerUserId, string newSessionId, CancellationToken cancellationToken);

    /// <summary>查询当前锁状态。</summary>
    Task<AppDraftLockInfo?> GetCurrentAsync(TenantId tenantId, long appId, CancellationToken cancellationToken);
}

public sealed record AppDraftLockResult(bool Acquired, AppDraftLockInfo? Lock);

public sealed record AppDraftLockInfo(long AppId, long OwnerUserId, string SessionId, DateTimeOffset AcquiredAt, DateTimeOffset LastRenewedAt);

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

    /// <summary>续约心跳；返回 Lost 表示锁已被他人夺取、过期或不存在。</summary>
    Task<AppDraftLockResult> RenewAsync(TenantId tenantId, long appId, string sessionId, CancellationToken cancellationToken);

    /// <summary>主动释放锁。</summary>
    Task ReleaseAsync(TenantId tenantId, long appId, string sessionId, CancellationToken cancellationToken);

    /// <summary>强制夺锁（高权限场景），写审计。</summary>
    Task<AppDraftLockResult> ForceTakeoverAsync(TenantId tenantId, long appId, long newOwnerUserId, string newSessionId, CancellationToken cancellationToken);

    /// <summary>查询当前锁状态。</summary>
    Task<AppDraftLockInfo?> GetCurrentAsync(TenantId tenantId, long appId, CancellationToken cancellationToken);

    /// <summary>校验指定编辑会话是否持有当前有效锁。</summary>
    Task<AppDraftLockValidationResult> ValidateAsync(TenantId tenantId, long appId, string? sessionId, CancellationToken cancellationToken);
}

public enum AppDraftLockStatus
{
    Acquired = 0,
    Conflict = 1,
    Recovered = 2,
    Lost = 3
}

public sealed record AppDraftLockResult(AppDraftLockStatus Status, AppDraftLockInfo? Lock)
{
    public bool Acquired => Status is AppDraftLockStatus.Acquired or AppDraftLockStatus.Recovered;
}

public sealed record AppDraftLockValidationResult(bool IsValid, AppDraftLockStatus Status, AppDraftLockInfo? Lock);

public sealed record AppDraftLockInfo(long AppId, long OwnerUserId, string SessionId, DateTimeOffset AcquiredAt, DateTimeOffset LastRenewedAt);

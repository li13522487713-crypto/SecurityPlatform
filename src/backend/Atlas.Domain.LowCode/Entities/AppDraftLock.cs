using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LowCode.Entities;

/// <summary>
/// 应用草稿稿锁（M04 S04-2）。
/// 一个应用同一时刻仅一条有效记录（由 (TenantId, AppId) 唯一索引保证）。
/// </summary>
public sealed class AppDraftLock : TenantEntity
{
#pragma warning disable CS8618
    public AppDraftLock()
        : base(TenantId.Empty)
    {
        SessionId = string.Empty;
    }
#pragma warning restore CS8618

    public AppDraftLock(TenantId tenantId, long id, long appId, long ownerUserId, string sessionId)
        : base(tenantId)
    {
        Id = id;
        AppId = appId;
        OwnerUserId = ownerUserId;
        SessionId = sessionId;
        AcquiredAt = DateTimeOffset.UtcNow;
        LastRenewedAt = AcquiredAt;
    }

    public long AppId { get; private set; }
    public long OwnerUserId { get; private set; }

    [SugarColumn(Length = 128, IsNullable = false)]
    public string SessionId { get; private set; }

    public DateTimeOffset AcquiredAt { get; private set; }
    public DateTimeOffset LastRenewedAt { get; private set; }

    public void Renew(string sessionId)
    {
        if (!string.Equals(sessionId, SessionId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("会话 ID 不匹配，禁止续约他人锁。");
        }
        LastRenewedAt = DateTimeOffset.UtcNow;
    }

    public void Takeover(long newOwnerUserId, string newSessionId)
    {
        OwnerUserId = newOwnerUserId;
        SessionId = newSessionId;
        AcquiredAt = DateTimeOffset.UtcNow;
        LastRenewedAt = AcquiredAt;
    }
}

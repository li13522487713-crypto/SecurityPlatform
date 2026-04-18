using Atlas.Application.Audit.Abstractions;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services.LowCode;

/// <summary>
/// 应用草稿稿锁服务实现（M04 S04-2）。
/// - 锁过期时间：60s（>=2 个 30s 心跳周期，给客户端容错空间）。
/// </summary>
public sealed class AppDraftLockService : IAppDraftLockService
{
    private static readonly TimeSpan LockTtl = TimeSpan.FromSeconds(60);

    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly IAuditWriter _auditWriter;

    public AppDraftLockService(ISqlSugarClient db, IIdGeneratorAccessor idGen, IAuditWriter auditWriter)
    {
        _db = db;
        _idGen = idGen;
        _auditWriter = auditWriter;
    }

    public async Task<AppDraftLockResult> TryAcquireAsync(TenantId tenantId, long appId, long userId, string sessionId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var existing = await _db.Queryable<AppDraftLock>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .FirstAsync(cancellationToken);

        if (existing is not null)
        {
            var expired = now - existing.LastRenewedAt > LockTtl;
            if (!expired && existing.OwnerUserId != userId)
            {
                return new AppDraftLockResult(false, ToInfo(existing));
            }
            // 同一持有者重新获取 / 已过期由本人接管
            existing.Takeover(userId, sessionId);
            await _db.Updateable(existing).ExecuteCommandAsync(cancellationToken);
            await _auditWriter.WriteAsync(new AuditRecord(tenantId, userId.ToString(), "lowcode.app.draft.lock.acquire", "success", $"app:{appId}", null, null), cancellationToken);
            return new AppDraftLockResult(true, ToInfo(existing));
        }

        var lockEntity = new AppDraftLock(tenantId, _idGen.NextId(), appId, userId, sessionId);
        await _db.Insertable(lockEntity).ExecuteCommandAsync(cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, userId.ToString(), "lowcode.app.draft.lock.acquire", "success", $"app:{appId}", null, null), cancellationToken);
        return new AppDraftLockResult(true, ToInfo(lockEntity));
    }

    public async Task<bool> RenewAsync(TenantId tenantId, long appId, string sessionId, CancellationToken cancellationToken)
    {
        var existing = await _db.Queryable<AppDraftLock>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .FirstAsync(cancellationToken);
        if (existing is null) return false;
        if (!string.Equals(existing.SessionId, sessionId, StringComparison.Ordinal)) return false;
        existing.Renew(sessionId);
        await _db.Updateable(existing).ExecuteCommandAsync(cancellationToken);
        return true;
    }

    public async Task ReleaseAsync(TenantId tenantId, long appId, string sessionId, CancellationToken cancellationToken)
    {
        var existing = await _db.Queryable<AppDraftLock>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .FirstAsync(cancellationToken);
        if (existing is null) return;
        if (!string.Equals(existing.SessionId, sessionId, StringComparison.Ordinal)) return;
        await _db.Deleteable<AppDraftLock>()
            .Where(x => x.Id == existing.Id && x.TenantIdValue == tenantId.Value)
            .ExecuteCommandAsync(cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, existing.OwnerUserId.ToString(), "lowcode.app.draft.lock.release", "success", $"app:{appId}", null, null), cancellationToken);
    }

    public async Task<AppDraftLockResult> ForceTakeoverAsync(TenantId tenantId, long appId, long newOwnerUserId, string newSessionId, CancellationToken cancellationToken)
    {
        var existing = await _db.Queryable<AppDraftLock>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .FirstAsync(cancellationToken);
        if (existing is null)
        {
            return await TryAcquireAsync(tenantId, appId, newOwnerUserId, newSessionId, cancellationToken);
        }
        existing.Takeover(newOwnerUserId, newSessionId);
        await _db.Updateable(existing).ExecuteCommandAsync(cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, newOwnerUserId.ToString(), "lowcode.app.draft.lock.takeover", "success", $"app:{appId}:from-user:{existing.OwnerUserId}", null, null), cancellationToken);
        return new AppDraftLockResult(true, ToInfo(existing));
    }

    public async Task<AppDraftLockInfo?> GetCurrentAsync(TenantId tenantId, long appId, CancellationToken cancellationToken)
    {
        var existing = await _db.Queryable<AppDraftLock>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .FirstAsync(cancellationToken);
        return existing is null ? null : ToInfo(existing);
    }

    private static AppDraftLockInfo ToInfo(AppDraftLock l) =>
        new(l.AppId, l.OwnerUserId, l.SessionId, l.AcquiredAt, l.LastRenewedAt);
}

using Atlas.Application.Audit.Abstractions;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Atlas.Domain.LowCode.Entities;
using Microsoft.Extensions.Hosting;
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
    private readonly IHostEnvironment _hostEnvironment;

    public AppDraftLockService(
        ISqlSugarClient db,
        IIdGeneratorAccessor idGen,
        IAuditWriter auditWriter,
        IHostEnvironment hostEnvironment)
    {
        _db = db;
        _idGen = idGen;
        _auditWriter = auditWriter;
        _hostEnvironment = hostEnvironment;
    }

    public async Task<AppDraftLockResult> TryAcquireAsync(TenantId tenantId, long appId, long userId, string sessionId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var existing = await _db.Queryable<AppDraftLock>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .FirstAsync(cancellationToken);

        if (existing is not null)
        {
            if (IsExpired(existing, now))
            {
                existing.Takeover(userId, sessionId);
                await UpdateLockAsync(tenantId, existing, appId, cancellationToken);
                await _auditWriter.WriteAsync(new AuditRecord(tenantId, userId.ToString(), "lowcode.app.draft.lock.recover", "success", $"app:{appId}", null, null), cancellationToken);
                return new AppDraftLockResult(AppDraftLockStatus.Recovered, ToInfo(existing));
            }

            if (string.Equals(existing.SessionId, sessionId, StringComparison.Ordinal))
            {
                existing.Renew(sessionId);
                await UpdateLockAsync(tenantId, existing, appId, cancellationToken);
                return new AppDraftLockResult(AppDraftLockStatus.Acquired, ToInfo(existing));
            }

            if (existing.OwnerUserId != userId || !_hostEnvironment.IsDevelopment())
            {
                return new AppDraftLockResult(AppDraftLockStatus.Conflict, ToInfo(existing));
            }

            // Development 下同一用户的 HMR / 同 Tab 重载 / 旧会话残留自动恢复。
            existing.Takeover(userId, sessionId);
            await UpdateLockAsync(tenantId, existing, appId, cancellationToken);
            await _auditWriter.WriteAsync(new AuditRecord(tenantId, userId.ToString(), "lowcode.app.draft.lock.recover", "success", $"app:{appId}", null, null), cancellationToken);
            return new AppDraftLockResult(AppDraftLockStatus.Recovered, ToInfo(existing));
        }

        var lockEntity = new AppDraftLock(tenantId, _idGen.NextId(), appId, userId, sessionId);
        await _db.Insertable(lockEntity).ExecuteCommandAsync(cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, userId.ToString(), "lowcode.app.draft.lock.acquire", "success", $"app:{appId}", null, null), cancellationToken);
        return new AppDraftLockResult(AppDraftLockStatus.Acquired, ToInfo(lockEntity));
    }

    public async Task<AppDraftLockResult> RenewAsync(TenantId tenantId, long appId, string sessionId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var existing = await _db.Queryable<AppDraftLock>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .FirstAsync(cancellationToken);
        if (existing is null)
        {
            return new AppDraftLockResult(AppDraftLockStatus.Lost, null);
        }
        if (IsExpired(existing, now))
        {
            await DeleteLockAsync(existing, tenantId, cancellationToken);
            return new AppDraftLockResult(AppDraftLockStatus.Lost, null);
        }
        if (!string.Equals(existing.SessionId, sessionId, StringComparison.Ordinal))
        {
            return new AppDraftLockResult(AppDraftLockStatus.Lost, ToInfo(existing));
        }

        existing.Renew(sessionId);
        var affectedRows = await _db.Updateable(existing)
            .Where(x => x.Id == existing.Id && x.TenantIdValue == tenantId.Value && x.AppId == appId && x.SessionId == sessionId)
            .ExecuteCommandAsync(cancellationToken);
        return affectedRows > 0
            ? new AppDraftLockResult(AppDraftLockStatus.Acquired, ToInfo(existing))
            : new AppDraftLockResult(AppDraftLockStatus.Lost, null);
    }

    public async Task ReleaseAsync(TenantId tenantId, long appId, string sessionId, CancellationToken cancellationToken)
    {
        var existing = await _db.Queryable<AppDraftLock>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .FirstAsync(cancellationToken);
        if (existing is null) return;
        if (!string.Equals(existing.SessionId, sessionId, StringComparison.Ordinal)) return;
        await DeleteLockAsync(existing, tenantId, cancellationToken);
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
        var previousOwnerUserId = existing.OwnerUserId;
        existing.Takeover(newOwnerUserId, newSessionId);
        await UpdateLockAsync(tenantId, existing, appId, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, newOwnerUserId.ToString(), "lowcode.app.draft.lock.takeover", "success", $"app:{appId}:from-user:{previousOwnerUserId}", null, null), cancellationToken);
        return new AppDraftLockResult(AppDraftLockStatus.Recovered, ToInfo(existing));
    }

    public async Task<AppDraftLockInfo?> GetCurrentAsync(TenantId tenantId, long appId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var existing = await _db.Queryable<AppDraftLock>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .FirstAsync(cancellationToken);
        if (existing is not null && IsExpired(existing, now))
        {
            await DeleteLockAsync(existing, tenantId, cancellationToken);
            return null;
        }
        return existing is null ? null : ToInfo(existing);
    }

    public async Task<AppDraftLockValidationResult> ValidateAsync(TenantId tenantId, long appId, string? sessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return new AppDraftLockValidationResult(false, AppDraftLockStatus.Lost, null);
        }

        var now = DateTimeOffset.UtcNow;
        var existing = await _db.Queryable<AppDraftLock>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .FirstAsync(cancellationToken);
        if (existing is null)
        {
            return new AppDraftLockValidationResult(false, AppDraftLockStatus.Lost, null);
        }
        if (IsExpired(existing, now))
        {
            await DeleteLockAsync(existing, tenantId, cancellationToken);
            return new AppDraftLockValidationResult(false, AppDraftLockStatus.Lost, null);
        }

        var matches = string.Equals(existing.SessionId, sessionId, StringComparison.Ordinal);
        return new AppDraftLockValidationResult(
            matches,
            matches ? AppDraftLockStatus.Acquired : AppDraftLockStatus.Conflict,
            ToInfo(existing));
    }

    private async Task UpdateLockAsync(TenantId tenantId, AppDraftLock entity, long appId, CancellationToken cancellationToken)
    {
        await _db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .ExecuteCommandAsync(cancellationToken);
    }

    private async Task DeleteLockAsync(AppDraftLock entity, TenantId tenantId, CancellationToken cancellationToken)
    {
        await _db.Deleteable<AppDraftLock>()
            .Where(x => x.Id == entity.Id && x.TenantIdValue == tenantId.Value)
            .ExecuteCommandAsync(cancellationToken);
    }

    private static bool IsExpired(AppDraftLock entity, DateTimeOffset now)
        => now - entity.LastRenewedAt > LockTtl;

    private static AppDraftLockInfo ToInfo(AppDraftLock l) =>
        new(l.AppId, l.OwnerUserId, l.SessionId, l.AcquiredAt, l.LastRenewedAt);
}

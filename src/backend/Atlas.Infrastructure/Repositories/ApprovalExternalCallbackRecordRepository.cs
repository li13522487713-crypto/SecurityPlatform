using Atlas.Application.Approval.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

/// <summary>
/// 外部回调记录仓储实现
/// </summary>
public sealed class ApprovalExternalCallbackRecordRepository : IApprovalExternalCallbackRecordRepository
{
    private readonly ISqlSugarClient _db;

    public ApprovalExternalCallbackRecordRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task AddAsync(ApprovalExternalCallbackRecord entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<ApprovalExternalCallbackRecord> entities, CancellationToken cancellationToken)
    {
        var list = entities.ToList();
        if (list.Count == 0)
        {
            return;
        }

        await _db.Insertable(list).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateAsync(ApprovalExternalCallbackRecord entity, CancellationToken cancellationToken)
    {
        await _db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateRangeAsync(IEnumerable<ApprovalExternalCallbackRecord> entities, CancellationToken cancellationToken)
    {
        var list = entities.ToList();
        if (list.Count == 0)
        {
            return;
        }

        await _db.Updateable(list).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<ApprovalExternalCallbackRecord?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalExternalCallbackRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<ApprovalExternalCallbackRecord?> GetByIdempotencyKeyAsync(
        TenantId tenantId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalExternalCallbackRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.IdempotencyKey == idempotencyKey)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalExternalCallbackRecord>> QueryByIdempotencyKeysAsync(
        TenantId tenantId,
        IReadOnlyList<string> idempotencyKeys,
        CancellationToken cancellationToken)
    {
        if (idempotencyKeys.Count == 0)
        {
            return Array.Empty<ApprovalExternalCallbackRecord>();
        }

        var distinctKeys = idempotencyKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (distinctKeys.Length == 0)
        {
            return Array.Empty<ApprovalExternalCallbackRecord>();
        }

        return await _db.Queryable<ApprovalExternalCallbackRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(distinctKeys, x.IdempotencyKey))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalExternalCallbackRecord>> GetPendingRetriesAsync(
        TenantId tenantId,
        DateTimeOffset currentTime,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalExternalCallbackRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && (x.Status == CallbackStatus.Pending || x.Status == CallbackStatus.Failed)
                && x.NextRetryAt <= currentTime)
            .OrderBy(x => x.NextRetryAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalExternalCallbackRecord>> GetByInstanceAsync(
        TenantId tenantId,
        long instanceId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalExternalCallbackRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.InstanceId == instanceId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}

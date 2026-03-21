using Atlas.Application.Metering;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Metering;
using SqlSugar;

namespace Atlas.Infrastructure.Services.Metering;

public sealed class MeteringService : IMeteringService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGen;

    public MeteringService(ISqlSugarClient db, IIdGeneratorAccessor idGen)
    {
        _db = db;
        _idGen = idGen;
    }

    public async Task RecordUsageAsync(
        TenantId tenantId,
        string resourceType,
        decimal quantity,
        string? unitLabel = null,
        CancellationToken cancellationToken = default)
    {
        var record = new UsageRecord(
            tenantId,
            _idGen.Generator.NextId(),
            resourceType,
            quantity,
            unitLabel,
            DateTimeOffset.UtcNow);
        await _db.Insertable(record).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<decimal> GetTotalUsageAsync(
        TenantId tenantId,
        string resourceType,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var result = await _db.Queryable<UsageRecord>()
            .Where(r => r.TenantIdValue == tenantValue
                     && r.ResourceType == resourceType
                     && r.RecordedAt >= from
                     && r.RecordedAt <= to)
            .SumAsync(r => r.Quantity);
        return result;
    }

    public async Task<bool> IsQuotaExceededAsync(
        TenantId tenantId,
        string resourceType,
        decimal additionalQuantity = 0,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var quota = await _db.Queryable<TenantQuota>()
            .Where(q => q.TenantIdValue == tenantValue && q.ResourceType == resourceType && q.IsEnabled)
            .FirstAsync(cancellationToken);

        if (quota is null)
        {
            return false;
        }

        // 当日累计用量：[今日 00:00:00.0000000 UTC, 今日 23:59:59.9999999 UTC]
        // 使用 AddTicks(-1) 而非 AddSeconds(-1)，确保亚秒级记录（如 23:59:59.5）也被纳入统计
        var todayUtc = new DateTimeOffset(DateTimeOffset.UtcNow.Date, TimeSpan.Zero);
        var usedToday = await GetTotalUsageAsync(tenantId, resourceType, todayUtc, todayUtc.AddDays(1).AddTicks(-1), cancellationToken);
        return usedToday + additionalQuantity > quota.MaxQuantity;
    }

    public async Task<IReadOnlyList<TenantQuota>> GetQuotasAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        return await _db.Queryable<TenantQuota>()
            .Where(q => q.TenantIdValue == tenantValue)
            .ToListAsync(cancellationToken);
    }

    public async Task SetQuotaAsync(
        TenantId tenantId,
        string resourceType,
        decimal maxQuantity,
        bool isEnabled = true,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var now = DateTimeOffset.UtcNow;
        var existing = await _db.Queryable<TenantQuota>()
            .Where(q => q.TenantIdValue == tenantValue && q.ResourceType == resourceType)
            .FirstAsync(cancellationToken);

        if (existing is not null)
        {
            existing.Update(maxQuantity, isEnabled, now);
            await _db.Updateable(existing).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            var quota = new TenantQuota(tenantId, _idGen.Generator.NextId(), resourceType, maxQuantity)
            {
                IsEnabled = isEnabled,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _db.Insertable(quota).ExecuteCommandAsync(cancellationToken);
        }
    }
}

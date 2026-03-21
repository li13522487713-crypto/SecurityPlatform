using Atlas.Application.Events;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Events;
using SqlSugar;

namespace Atlas.Infrastructure.Events;

public sealed class PlatformEventService : IPlatformEventService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGen;

    public PlatformEventService(ISqlSugarClient db, IIdGeneratorAccessor idGen)
    {
        _db = db;
        _idGen = idGen;
    }

    public async Task PublishAsync(
        TenantId tenantId,
        string eventType,
        string source,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var evt = new PlatformEvent(tenantId, _idGen.Generator.NextId(), eventType, source, payloadJson, now);
        await _db.Insertable(evt).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PlatformEvent>> QueryAsync(
        TenantId tenantId,
        string? eventType,
        bool? isProcessed,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var idx = pageIndex <= 0 ? 1 : pageIndex;
        var size = pageSize is <= 0 or > 200 ? 20 : pageSize;

        var query = _db.Queryable<PlatformEvent>()
            .Where(e => e.TenantIdValue == tenantValue);

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query = query.Where(e => e.EventType == eventType);
        }

        if (isProcessed.HasValue)
        {
            query = query.Where(e => e.IsProcessed == isProcessed.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(e => e.OccurredAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(e => e.OccurredAt <= to.Value);
        }

        return await query
            .OrderByDescending(e => e.OccurredAt)
            .ToPageListAsync(idx, size, cancellationToken);
    }
}

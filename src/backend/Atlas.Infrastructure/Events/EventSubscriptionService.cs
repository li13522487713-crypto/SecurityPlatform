using Atlas.Application.Events;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Events;
using SqlSugar;

namespace Atlas.Infrastructure.Events;

public sealed class EventSubscriptionService : IEventSubscriptionService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly ITenantProvider _tenantProvider;

    public EventSubscriptionService(ISqlSugarClient db, IIdGeneratorAccessor idGen, ITenantProvider tenantProvider)
    {
        _db = db;
        _idGen = idGen;
        _tenantProvider = tenantProvider;
    }

    public async Task<IReadOnlyList<EventSubscription>> GetAllAsync(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId.Value;
        return await _db.Queryable<EventSubscription>()
            .Where(s => s.TenantIdValue == tenantId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<EventSubscription?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId.Value;
        return await _db.Queryable<EventSubscription>()
            .Where(s => s.Id == id && s.TenantIdValue == tenantId)
            .FirstAsync(cancellationToken);
    }

    public async Task<long> CreateAsync(CreateEventSubscriptionRequest request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var sub = new EventSubscription
        {
            Id = _idGen.Generator.NextId(),
            TenantId = _tenantProvider.TenantId.Value,
            Name = request.Name,
            EventTypePattern = request.EventTypePattern,
            TargetType = request.TargetType,
            TargetConfig = request.TargetConfig,
            FilterExpression = request.FilterExpression,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _db.Insertable(sub).ExecuteCommandAsync(cancellationToken);
        return sub.Id;
    }

    public async Task UpdateAsync(long id, UpdateEventSubscriptionRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId.Value;
        await _db.Updateable<EventSubscription>()
            .SetColumns(s => new EventSubscription
            {
                Name = request.Name,
                EventTypePattern = request.EventTypePattern,
                TargetType = request.TargetType,
                TargetConfig = request.TargetConfig,
                FilterExpression = request.FilterExpression,
                IsActive = request.IsActive,
                UpdatedAt = DateTimeOffset.UtcNow
            })
            .Where(s => s.Id == id && s.TenantIdValue == tenantId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId.Value;
        await _db.Deleteable<EventSubscription>().Where(s => s.Id == id && s.TenantIdValue == tenantId).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EventSubscription>> GetMatchingAsync(string eventType, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId.Value;
        var all = await _db.Queryable<EventSubscription>()
            .Where(s => s.IsActive && s.TenantIdValue == tenantId)
            .ToListAsync(cancellationToken);

        return all.Where(s =>
            s.EventTypePattern == "*" ||
            s.EventTypePattern == eventType ||
            (s.EventTypePattern.EndsWith("*") && eventType.StartsWith(s.EventTypePattern[..^1]))).ToList();
    }
}

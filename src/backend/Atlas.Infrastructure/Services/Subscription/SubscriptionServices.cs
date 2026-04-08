using Atlas.Application.Subscription;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Subscription;
using Atlas.Infrastructure.Caching;
using SqlSugar;

namespace Atlas.Infrastructure.Services.Subscription;

public sealed class PlanService : IPlanQueryService, IPlanCommandService
{
    private static readonly TimeSpan PlanCacheTtl = TimeSpan.FromMinutes(30);

    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly IAtlasHybridCache _cache;

    public PlanService(ISqlSugarClient db, IIdGeneratorAccessor idGen, IAtlasHybridCache cache)
    {
        _db = db;
        _idGen = idGen;
        _cache = cache;
    }

    public async Task<IReadOnlyList<Plan>> GetActivePlansAsync(CancellationToken cancellationToken = default)
    {
        var key = AtlasCacheKeys.Plans.ActiveList();
        return await _cache.GetOrCreateAsync<IReadOnlyList<Plan>>(
                   key,
                   async ct => await _db.Queryable<Plan>()
                       .Where(p => p.IsActive)
                       .OrderBy(p => p.MonthlyPrice)
                       .ToListAsync(ct),
                   PlanCacheTtl,
                   [AtlasCacheTags.Plans()],
                   cancellationToken: cancellationToken)
               ?? Array.Empty<Plan>();
    }

    public async Task<Plan?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var key = AtlasCacheKeys.Plans.ById(id);
        return await _cache.GetOrCreateAsync(
            key,
            async ct => await _db.Queryable<Plan>().Where(p => p.Id == id).FirstAsync(ct),
            PlanCacheTtl,
            [AtlasCacheTags.Plans()],
            cancellationToken: cancellationToken);
    }

    public async Task<Plan?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var key = AtlasCacheKeys.Plans.ByCode(code);
        return await _cache.GetOrCreateAsync(
            key,
            async ct => await _db.Queryable<Plan>().Where(p => p.Code == code).FirstAsync(ct),
            PlanCacheTtl,
            [AtlasCacheTags.Plans()],
            cancellationToken: cancellationToken);
    }

    public async Task<long> CreateAsync(CreatePlanRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var plan = new Plan(_idGen.Generator.NextId(), request.Code, request.Name, request.Description)
        {
            MonthlyPrice = request.MonthlyPrice,
            QuotaJson = request.QuotaJson,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _db.Insertable(plan).ExecuteCommandAsync(cancellationToken);
        await InvalidatePlansAsync(cancellationToken);
        return plan.Id;
    }

    public async Task UpdateAsync(long id, UpdatePlanRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        await _db.Updateable<Plan>()
            .SetColumns(p => new Plan
            {
                Name = request.Name,
                Description = request.Description,
                MonthlyPrice = request.MonthlyPrice,
                QuotaJson = request.QuotaJson,
                IsActive = request.IsActive,
                UpdatedAt = now
            })
            .Where(p => p.Id == id)
            .ExecuteCommandAsync(cancellationToken);
        await InvalidatePlansAsync(cancellationToken);
    }

    public async Task DeactivateAsync(long id, CancellationToken cancellationToken = default)
    {
        await _db.Updateable<Plan>()
            .SetColumns(p => new Plan { IsActive = false, UpdatedAt = DateTimeOffset.UtcNow })
            .Where(p => p.Id == id)
            .ExecuteCommandAsync(cancellationToken);
        await InvalidatePlansAsync(cancellationToken);
    }

    private async Task InvalidatePlansAsync(CancellationToken cancellationToken)
    {
        await _cache.RemoveByTagAsync(AtlasCacheTags.Plans(), cancellationToken);
    }
}

public sealed class SubscriptionService : ISubscriptionService
{
    private static readonly TimeSpan SubscriptionCacheTtl = TimeSpan.FromMinutes(5);

    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly IAtlasHybridCache _cache;

    public SubscriptionService(ISqlSugarClient db, IIdGeneratorAccessor idGen, IAtlasHybridCache cache)
    {
        _db = db;
        _idGen = idGen;
        _cache = cache;
    }

    public async Task<TenantSubscription?> GetCurrentAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var key = AtlasCacheKeys.Subscriptions.Current(tenantId);
        return await _cache.GetOrCreateAsync(
            key,
            async ct => await _db.Queryable<TenantSubscription>()
                .Where(s => s.TenantIdValue == tenantValue && s.Status == SubscriptionStatus.Active)
                .OrderByDescending(s => s.CreatedAt)
                .FirstAsync(ct),
            SubscriptionCacheTtl,
            [AtlasCacheTags.SubscriptionTenant(tenantId)],
            cancellationToken: cancellationToken);
    }

    public async Task<long> SubscribeAsync(TenantId tenantId, long planId, DateTimeOffset? expiresAt, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var sub = new TenantSubscription(tenantId, _idGen.Generator.NextId(), planId, now, expiresAt)
        {
            CreatedAt = now,
            UpdatedAt = now
        };
        await _db.Insertable(sub).ExecuteCommandAsync(cancellationToken);
        await InvalidateSubscriptionAsync(tenantId, cancellationToken);
        return sub.Id;
    }

    public async Task CancelAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var now = DateTimeOffset.UtcNow;
        var current = await _db.Queryable<TenantSubscription>()
            .Where(s => s.TenantIdValue == tenantValue && s.Status == SubscriptionStatus.Active)
            .FirstAsync(cancellationToken);
        if (current is null) return;

        current.Cancel(now);
        await _db.Updateable(current).ExecuteCommandAsync(cancellationToken);
        await InvalidateSubscriptionAsync(tenantId, cancellationToken);
    }

    public async Task RenewAsync(TenantId tenantId, DateTimeOffset newExpiresAt, CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var now = DateTimeOffset.UtcNow;
        var current = await _db.Queryable<TenantSubscription>()
            .Where(s => s.TenantIdValue == tenantValue && s.Status == SubscriptionStatus.Active)
            .FirstAsync(cancellationToken);
        if (current is null) return;

        current.Renew(newExpiresAt, now);
        await _db.Updateable(current).ExecuteCommandAsync(cancellationToken);
        await InvalidateSubscriptionAsync(tenantId, cancellationToken);
    }

    private async Task InvalidateSubscriptionAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        await _cache.RemoveByTagAsync(AtlasCacheTags.SubscriptionTenant(tenantId), cancellationToken);
    }
}

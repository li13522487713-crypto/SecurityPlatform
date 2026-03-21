using Atlas.Application.Subscription;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Subscription;
using SqlSugar;

namespace Atlas.Infrastructure.Services.Subscription;

public sealed class PlanService : IPlanQueryService, IPlanCommandService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGen;

    public PlanService(ISqlSugarClient db, IIdGeneratorAccessor idGen)
    {
        _db = db;
        _idGen = idGen;
    }

    public async Task<IReadOnlyList<Plan>> GetActivePlansAsync(CancellationToken cancellationToken = default)
        => await _db.Queryable<Plan>()
            .Where(p => p.IsActive)
            .OrderBy(p => p.MonthlyPrice)
            .ToListAsync(cancellationToken);

    public async Task<Plan?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => await _db.Queryable<Plan>().Where(p => p.Id == id).FirstAsync(cancellationToken);

    public async Task<Plan?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => await _db.Queryable<Plan>().Where(p => p.Code == code).FirstAsync(cancellationToken);

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
    }

    public async Task DeactivateAsync(long id, CancellationToken cancellationToken = default)
    {
        await _db.Updateable<Plan>()
            .SetColumns(p => new Plan { IsActive = false, UpdatedAt = DateTimeOffset.UtcNow })
            .Where(p => p.Id == id)
            .ExecuteCommandAsync(cancellationToken);
    }
}

public sealed class SubscriptionService : ISubscriptionService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGen;

    public SubscriptionService(ISqlSugarClient db, IIdGeneratorAccessor idGen)
    {
        _db = db;
        _idGen = idGen;
    }

    public async Task<TenantSubscription?> GetCurrentAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        return await _db.Queryable<TenantSubscription>()
            .Where(s => s.TenantIdValue == tenantValue && s.Status == SubscriptionStatus.Active)
            .OrderByDescending(s => s.CreatedAt)
            .FirstAsync(cancellationToken);
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
    }
}

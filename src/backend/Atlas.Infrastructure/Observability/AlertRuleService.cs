using Atlas.Application.Observability;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Observability;
using SqlSugar;

namespace Atlas.Infrastructure.Observability;

public sealed class AlertRuleService : IAlertRuleService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGen;

    public AlertRuleService(ISqlSugarClient db, IIdGeneratorAccessor idGen)
    {
        _db = db;
        _idGen = idGen;
    }

    public async Task<IReadOnlyList<AlertRule>> GetAllAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        return await _db.Queryable<AlertRule>()
            .Where(r => r.TenantIdValue == tenantValue)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<long> CreateAsync(TenantId tenantId, CreateAlertRuleRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var rule = new AlertRule(tenantId, _idGen.Generator.NextId(), request.Name, request.MetricName)
        {
            Operator = request.Operator,
            Threshold = request.Threshold,
            WindowMinutes = request.WindowMinutes,
            EventType = request.EventType,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _db.Insertable(rule).ExecuteCommandAsync(cancellationToken);
        return rule.Id;
    }

    public async Task UpdateAsync(TenantId tenantId, long id, UpdateAlertRuleRequest request, CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        await _db.Updateable<AlertRule>()
            .SetColumns(r => new AlertRule
            {
                Name = request.Name,
                Operator = request.Operator,
                Threshold = request.Threshold,
                WindowMinutes = request.WindowMinutes,
                EventType = request.EventType,
                IsActive = request.IsActive,
                UpdatedAt = DateTimeOffset.UtcNow
            })
            .Where(r => r.Id == id && r.TenantIdValue == tenantValue)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        await _db.Deleteable<AlertRule>()
            .Where(r => r.Id == id && r.TenantIdValue == tenantValue)
            .ExecuteCommandAsync(cancellationToken);
    }
}

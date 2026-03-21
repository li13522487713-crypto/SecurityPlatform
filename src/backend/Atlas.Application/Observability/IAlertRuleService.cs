using Atlas.Core.Tenancy;
using Atlas.Domain.Observability;

namespace Atlas.Application.Observability;

public interface IAlertRuleService
{
    Task<IReadOnlyList<AlertRule>> GetAllAsync(TenantId tenantId, CancellationToken cancellationToken = default);
    Task<long> CreateAsync(TenantId tenantId, CreateAlertRuleRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(TenantId tenantId, long id, UpdateAlertRuleRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default);
}

public sealed record CreateAlertRuleRequest(
    string Name,
    string MetricName,
    string Operator,
    decimal Threshold,
    int WindowMinutes,
    string EventType);

public sealed record UpdateAlertRuleRequest(
    string Name,
    string Operator,
    decimal Threshold,
    int WindowMinutes,
    string EventType,
    bool IsActive);

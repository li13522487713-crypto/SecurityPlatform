using Atlas.Core.Tenancy;
using Atlas.Domain.Subscription;

namespace Atlas.Application.Subscription;

/// <summary>
/// 套餐查询服务
/// </summary>
public interface IPlanQueryService
{
    Task<IReadOnlyList<Plan>> GetActivePlansAsync(CancellationToken cancellationToken = default);
    Task<Plan?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Plan?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
}

/// <summary>
/// 套餐管理服务（平台管理员）
/// </summary>
public interface IPlanCommandService
{
    Task<long> CreateAsync(CreatePlanRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(long id, UpdatePlanRequest request, CancellationToken cancellationToken = default);
    Task DeactivateAsync(long id, CancellationToken cancellationToken = default);
}

/// <summary>
/// 租户订阅服务
/// </summary>
public interface ISubscriptionService
{
    Task<TenantSubscription?> GetCurrentAsync(TenantId tenantId, CancellationToken cancellationToken = default);
    Task<long> SubscribeAsync(TenantId tenantId, long planId, DateTimeOffset? expiresAt, CancellationToken cancellationToken = default);
    Task CancelAsync(TenantId tenantId, CancellationToken cancellationToken = default);
    Task RenewAsync(TenantId tenantId, DateTimeOffset newExpiresAt, CancellationToken cancellationToken = default);
}

public sealed record CreatePlanRequest(
    string Code,
    string Name,
    string Description,
    decimal MonthlyPrice,
    string QuotaJson);

public sealed record UpdatePlanRequest(
    string Name,
    string Description,
    decimal MonthlyPrice,
    string QuotaJson,
    bool IsActive);

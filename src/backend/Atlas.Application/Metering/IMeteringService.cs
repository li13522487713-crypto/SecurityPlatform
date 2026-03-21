using Atlas.Core.Tenancy;
using Atlas.Domain.Metering;

namespace Atlas.Application.Metering;

/// <summary>
/// 计量与配额服务接口
/// </summary>
public interface IMeteringService
{
    /// <summary>记录一次资源使用（增量）</summary>
    Task RecordUsageAsync(TenantId tenantId, string resourceType, decimal quantity, string? unitLabel = null, CancellationToken cancellationToken = default);

    /// <summary>查询指定时间段内某租户某资源类型的累计用量</summary>
    Task<decimal> GetTotalUsageAsync(TenantId tenantId, string resourceType, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

    /// <summary>检查是否已超配额（未配置时返回 false）</summary>
    Task<bool> IsQuotaExceededAsync(TenantId tenantId, string resourceType, decimal additionalQuantity = 0, CancellationToken cancellationToken = default);

    /// <summary>获取租户所有配额配置</summary>
    Task<IReadOnlyList<TenantQuota>> GetQuotasAsync(TenantId tenantId, CancellationToken cancellationToken = default);

    /// <summary>设置或更新租户配额</summary>
    Task SetQuotaAsync(TenantId tenantId, string resourceType, decimal maxQuantity, bool isEnabled = true, CancellationToken cancellationToken = default);
}

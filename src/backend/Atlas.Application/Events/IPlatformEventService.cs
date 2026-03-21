using Atlas.Core.Tenancy;
using Atlas.Domain.Events;

namespace Atlas.Application.Events;

/// <summary>
/// 平台统一事件发布与查询服务
/// </summary>
public interface IPlatformEventService
{
    /// <summary>发布一个平台事件（异步写入持久化，匹配订阅触发分发）</summary>
    Task PublishAsync(TenantId tenantId, string eventType, string source, string payloadJson, CancellationToken cancellationToken = default);

    /// <summary>分页查询平台事件历史</summary>
    Task<IReadOnlyList<PlatformEvent>> QueryAsync(
        TenantId tenantId,
        string? eventType,
        bool? isProcessed,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);
}

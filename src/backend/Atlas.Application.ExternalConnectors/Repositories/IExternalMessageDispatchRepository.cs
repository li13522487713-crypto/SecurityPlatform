using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Entities;
using Atlas.Domain.ExternalConnectors.Enums;

namespace Atlas.Application.ExternalConnectors.Repositories;

/// <summary>
/// 消息派发记录仓储：每发一条文本/卡片落一行，update_template_card / recall 复用同一行追加状态。
/// </summary>
public interface IExternalMessageDispatchRepository
{
    Task AddAsync(ExternalMessageDispatch entity, CancellationToken cancellationToken);

    Task UpdateAsync(ExternalMessageDispatch entity, CancellationToken cancellationToken);

    Task<ExternalMessageDispatch?> GetAsync(TenantId tenantId, long id, CancellationToken cancellationToken);

    /// <summary>按业务键查最近的一条派发，便于卡片更新链路。</summary>
    Task<ExternalMessageDispatch?> GetLatestByBusinessKeyAsync(TenantId tenantId, long providerId, string businessKey, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExternalMessageDispatch>> ListByProviderAsync(TenantId tenantId, long providerId, MessageDispatchStatus? status, int skip, int take, CancellationToken cancellationToken);
}

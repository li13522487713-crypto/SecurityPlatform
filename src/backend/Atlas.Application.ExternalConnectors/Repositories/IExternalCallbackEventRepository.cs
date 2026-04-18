using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Entities;
using Atlas.Domain.ExternalConnectors.Enums;

namespace Atlas.Application.ExternalConnectors.Repositories;

public interface IExternalCallbackEventRepository
{
    Task AddAsync(ExternalCallbackEvent entity, CancellationToken cancellationToken);

    Task UpdateAsync(ExternalCallbackEvent entity, CancellationToken cancellationToken);

    Task<ExternalCallbackEvent?> GetByIdempotencyAsync(TenantId tenantId, long providerId, string idempotencyKey, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExternalCallbackEvent>> ListPendingRetryAsync(TenantId tenantId, int take, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExternalCallbackEvent>> ListByStatusAsync(TenantId tenantId, long providerId, CallbackInboxStatus? status, int skip, int take, CancellationToken cancellationToken);
}

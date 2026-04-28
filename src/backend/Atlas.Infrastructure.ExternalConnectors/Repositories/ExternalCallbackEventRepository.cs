using Atlas.Application.ExternalConnectors.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Entities;
using Atlas.Domain.ExternalConnectors.Enums;
using SqlSugar;

namespace Atlas.Infrastructure.ExternalConnectors.Repositories;

public sealed class ExternalCallbackEventRepository : IExternalCallbackEventRepository
{
    private readonly ISqlSugarClient _db;

    public ExternalCallbackEventRepository(ISqlSugarClient db) { _db = db; }

    public async Task AddAsync(ExternalCallbackEvent entity, CancellationToken cancellationToken)
        => await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);

    public async Task UpdateAsync(ExternalCallbackEvent entity, CancellationToken cancellationToken)
        => await _db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);

    public async Task<ExternalCallbackEvent?> GetByIdempotencyAsync(TenantId tenantId, long providerId, string idempotencyKey, CancellationToken cancellationToken)
        => await _db.Queryable<ExternalCallbackEvent>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ProviderId == providerId && x.IdempotencyKey == idempotencyKey)
            .FirstAsync(cancellationToken);

    public async Task<IReadOnlyList<ExternalCallbackEvent>> ListPendingRetryAsync(TenantId tenantId, int take, CancellationToken cancellationToken)
        => await _db.Queryable<ExternalCallbackEvent>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Status == CallbackInboxStatus.Failed && x.NextRetryAt != null && x.NextRetryAt <= DateTimeOffset.UtcNow)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ExternalCallbackEvent>> ListByStatusAsync(TenantId tenantId, long providerId, CallbackInboxStatus? status, int skip, int take, CancellationToken cancellationToken)
    {
        var query = _db.Queryable<ExternalCallbackEvent>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ProviderId == providerId);
        if (status is { } s)
        {
            query = query.Where(x => x.Status == s);
        }
        return await query.OrderBy(x => x.ReceivedAt, OrderByType.Desc).Skip(skip).Take(take).ToListAsync(cancellationToken);
    }
}

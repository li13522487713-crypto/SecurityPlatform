using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Application.Abstractions;

public interface IIdempotencyRecordRepository
{
    Task<IdempotencyRecord?> FindActiveAsync(
        TenantId tenantId,
        long userId,
        string apiName,
        string idempotencyKey,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<bool> TryAddAsync(IdempotencyRecord record, CancellationToken cancellationToken);

    Task UpdateAsync(IdempotencyRecord record, CancellationToken cancellationToken);

    Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken);

    Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken);
}

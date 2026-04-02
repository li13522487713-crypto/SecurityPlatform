using Atlas.Core.Tenancy;

namespace Atlas.Application.BatchProcess.Abstractions;

public interface IBatchDeadLetterCommandService
{
    Task RetryAsync(long id, TenantId tenantId, CancellationToken cancellationToken);
    Task RetryBatchAsync(IReadOnlyList<long> ids, TenantId tenantId, CancellationToken cancellationToken);
    Task AbandonAsync(long id, TenantId tenantId, CancellationToken cancellationToken);
    Task AbandonBatchAsync(IReadOnlyList<long> ids, TenantId tenantId, CancellationToken cancellationToken);
}

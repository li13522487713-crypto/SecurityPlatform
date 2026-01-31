using Atlas.Application.DynamicTables.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.DynamicTables.Abstractions;

public interface IDynamicRecordCommandService
{
    Task<long> CreateAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        DynamicRecordUpsertRequest request,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        long id,
        DynamicRecordUpsertRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        long id,
        CancellationToken cancellationToken);

    Task DeleteBatchAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken);
}

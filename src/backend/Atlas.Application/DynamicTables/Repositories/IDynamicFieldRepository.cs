using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;

namespace Atlas.Application.DynamicTables.Repositories;

public interface IDynamicFieldRepository
{
    Task<IReadOnlyList<DynamicField>> ListByTableIdAsync(
        TenantId tenantId,
        long tableId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DynamicField>> ListByTableIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> tableIds,
        CancellationToken cancellationToken);

    Task AddRangeAsync(IReadOnlyList<DynamicField> fields, CancellationToken cancellationToken);

    Task UpdateRangeAsync(IReadOnlyList<DynamicField> fields, CancellationToken cancellationToken);

    Task DeleteByTableIdAsync(TenantId tenantId, long tableId, CancellationToken cancellationToken);
}

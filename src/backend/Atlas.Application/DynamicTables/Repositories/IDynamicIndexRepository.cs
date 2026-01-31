using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;

namespace Atlas.Application.DynamicTables.Repositories;

public interface IDynamicIndexRepository
{
    Task<IReadOnlyList<DynamicIndex>> ListByTableIdAsync(
        TenantId tenantId,
        long tableId,
        CancellationToken cancellationToken);

    Task AddRangeAsync(IReadOnlyList<DynamicIndex> indexes, CancellationToken cancellationToken);

    Task DeleteByTableIdAsync(TenantId tenantId, long tableId, CancellationToken cancellationToken);
}

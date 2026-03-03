using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;

namespace Atlas.Application.DynamicTables.Repositories;

public interface IDynamicRelationRepository
{
    Task<IReadOnlyList<DynamicRelation>> ListByTableIdAsync(
        TenantId tenantId,
        long tableId,
        CancellationToken cancellationToken);

    Task AddRangeAsync(
        IReadOnlyList<DynamicRelation> relations,
        CancellationToken cancellationToken);

    Task DeleteByTableIdAsync(
        TenantId tenantId,
        long tableId,
        CancellationToken cancellationToken);
}

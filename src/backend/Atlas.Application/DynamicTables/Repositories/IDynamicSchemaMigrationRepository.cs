using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;

namespace Atlas.Application.DynamicTables.Repositories;

public interface IDynamicSchemaMigrationRepository
{
    Task<(IReadOnlyList<DynamicSchemaMigration> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        long tableId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task AddAsync(DynamicSchemaMigration migration, CancellationToken cancellationToken);

    Task<DynamicSchemaMigration?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);
}

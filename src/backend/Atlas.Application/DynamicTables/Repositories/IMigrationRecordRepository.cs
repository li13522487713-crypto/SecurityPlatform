using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;

namespace Atlas.Application.DynamicTables.Repositories;

public interface IMigrationRecordRepository
{
    Task<(IReadOnlyList<MigrationRecord> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        string? keyword,
        string? tableKey,
        CancellationToken cancellationToken);

    Task<MigrationRecord?> FindByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<MigrationRecord?> FindByVersionAsync(
        TenantId tenantId,
        string tableKey,
        int version,
        CancellationToken cancellationToken);

    Task AddAsync(MigrationRecord entity, CancellationToken cancellationToken);
    Task UpdateAsync(MigrationRecord entity, CancellationToken cancellationToken);
}

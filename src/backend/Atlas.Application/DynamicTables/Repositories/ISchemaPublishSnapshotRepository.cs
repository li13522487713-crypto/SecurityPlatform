using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;

namespace Atlas.Application.DynamicTables.Repositories;

public interface ISchemaPublishSnapshotRepository
{
    Task<SchemaPublishSnapshot?> FindByIdAsync(
        TenantId tenantId,
        long snapshotId,
        CancellationToken cancellationToken);

    Task<SchemaPublishSnapshot?> FindLatestByTableAsync(
        TenantId tenantId,
        string tableKey,
        CancellationToken cancellationToken);

    Task<SchemaPublishSnapshot?> FindByVersionAsync(
        TenantId tenantId,
        string tableKey,
        int version,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<SchemaPublishSnapshot> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        string? tableKey,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task AddAsync(SchemaPublishSnapshot snapshot, CancellationToken cancellationToken);
}

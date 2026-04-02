using Atlas.Application.DynamicTables.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.DynamicTables.Abstractions;

public interface ISchemaPublishSnapshotQueryService
{
    Task<PagedResult<SchemaPublishSnapshotListItem>> QueryAsync(
        TenantId tenantId,
        string? tableKey,
        PagedRequest request,
        CancellationToken cancellationToken);

    Task<SchemaPublishSnapshotDetail?> GetByIdAsync(
        TenantId tenantId,
        long snapshotId,
        CancellationToken cancellationToken);

    Task<SchemaPublishSnapshotDetail?> GetLatestAsync(
        TenantId tenantId,
        string tableKey,
        CancellationToken cancellationToken);

    Task<SchemaSnapshotDiffResult?> DiffAsync(
        TenantId tenantId,
        string tableKey,
        int fromVersion,
        int toVersion,
        CancellationToken cancellationToken);
}

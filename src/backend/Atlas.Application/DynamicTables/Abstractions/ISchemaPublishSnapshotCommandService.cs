using Atlas.Application.DynamicTables.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.DynamicTables.Abstractions;

public interface ISchemaPublishSnapshotCommandService
{
    Task<long> CreateSnapshotAsync(
        TenantId tenantId,
        long userId,
        SchemaPublishSnapshotCreateRequest request,
        CancellationToken cancellationToken);
}

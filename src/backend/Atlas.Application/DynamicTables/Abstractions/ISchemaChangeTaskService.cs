using Atlas.Application.DynamicTables.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.DynamicTables.Abstractions;

public interface ISchemaChangeTaskService
{
    Task<IReadOnlyList<SchemaChangeTaskListItem>> ListByAppAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken);

    Task<SchemaChangeTaskListItem?> GetByIdAsync(
        TenantId tenantId,
        long taskId,
        CancellationToken cancellationToken);

    Task<long> CreateAndExecuteAsync(
        TenantId tenantId,
        long userId,
        SchemaChangeTaskCreateRequest request,
        CancellationToken cancellationToken);

    Task CancelAsync(
        TenantId tenantId,
        long taskId,
        CancellationToken cancellationToken);
}

using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;

namespace Atlas.Application.DynamicTables.Repositories;

public interface ISchemaChangeTaskRepository
{
    Task<IReadOnlyList<SchemaChangeTask>> ListByAppInstanceAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken);

    Task<SchemaChangeTask?> FindByIdAsync(
        TenantId tenantId,
        long taskId,
        CancellationToken cancellationToken);

    Task AddAsync(SchemaChangeTask task, CancellationToken cancellationToken);

    Task UpdateAsync(SchemaChangeTask task, CancellationToken cancellationToken);
}

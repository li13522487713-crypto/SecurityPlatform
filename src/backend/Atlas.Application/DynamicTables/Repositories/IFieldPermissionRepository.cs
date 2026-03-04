using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;

namespace Atlas.Application.DynamicTables.Repositories;

public interface IFieldPermissionRepository
{
    Task<IReadOnlyList<FieldPermission>> ListByTableKeyAsync(
        TenantId tenantId,
        string tableKey,
        CancellationToken cancellationToken);

    Task ReplaceByTableKeyAsync(
        TenantId tenantId,
        string tableKey,
        IReadOnlyList<FieldPermission> permissions,
        CancellationToken cancellationToken);
}

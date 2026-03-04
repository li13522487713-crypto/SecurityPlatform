using Atlas.Application.DynamicTables.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.DynamicTables.Abstractions;

public interface IDynamicTableQueryService
{
    Task<PagedResult<DynamicTableListItem>> QueryAsync(
        PagedRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<DynamicTableDetail?> GetByKeyAsync(
        TenantId tenantId,
        string tableKey,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DynamicFieldDefinition>> GetFieldsAsync(
        TenantId tenantId,
        string tableKey,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DynamicFieldTypeOption>> GetFieldTypesAsync(
        string dbType,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DynamicRelationDefinition>> GetRelationsAsync(
        TenantId tenantId,
        string tableKey,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DynamicFieldPermissionRule>> GetFieldPermissionsAsync(
        TenantId tenantId,
        string tableKey,
        CancellationToken cancellationToken);
}

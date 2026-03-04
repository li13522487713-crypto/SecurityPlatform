using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;

namespace Atlas.Application.DynamicTables.Abstractions;

public interface IFieldPermissionResolver
{
    Task<IReadOnlyList<DynamicField>> FilterViewableFieldsAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        IReadOnlyList<DynamicField> fields,
        CancellationToken cancellationToken);

    Task EnsureEditableFieldsAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        IReadOnlyList<string> fieldsToEdit,
        CancellationToken cancellationToken);
}

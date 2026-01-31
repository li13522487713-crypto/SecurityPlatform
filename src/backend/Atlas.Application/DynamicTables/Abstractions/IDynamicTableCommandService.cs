using Atlas.Application.DynamicTables.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.DynamicTables.Abstractions;

public interface IDynamicTableCommandService
{
    Task<long> CreateAsync(
        TenantId tenantId,
        long userId,
        DynamicTableCreateRequest request,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        DynamicTableUpdateRequest request,
        CancellationToken cancellationToken);

    Task AlterAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        DynamicTableAlterRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        CancellationToken cancellationToken);
}

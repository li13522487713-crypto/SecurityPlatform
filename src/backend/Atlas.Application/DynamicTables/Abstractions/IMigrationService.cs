using Atlas.Application.DynamicTables.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.DynamicTables.Abstractions;

public interface IMigrationService
{
    Task<PagedResult<MigrationRecordListItem>> QueryAsync(
        PagedRequest request,
        TenantId tenantId,
        string? tableKey,
        CancellationToken cancellationToken);

    Task<MigrationRecordDetail?> GetByIdAsync(
        TenantId tenantId,
        long migrationId,
        CancellationToken cancellationToken);

    Task<long> CreateAsync(
        TenantId tenantId,
        long userId,
        MigrationRecordCreateRequest request,
        CancellationToken cancellationToken);
}

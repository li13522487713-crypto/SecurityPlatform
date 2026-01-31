using Atlas.Application.DynamicTables.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.DynamicTables.Abstractions;

public interface IDynamicRecordQueryService
{
    Task<DynamicRecordListResult> QueryAsync(
        TenantId tenantId,
        string tableKey,
        DynamicRecordQueryRequest request,
        CancellationToken cancellationToken);

    Task<DynamicRecordDto?> GetByIdAsync(
        TenantId tenantId,
        string tableKey,
        long id,
        CancellationToken cancellationToken);
}

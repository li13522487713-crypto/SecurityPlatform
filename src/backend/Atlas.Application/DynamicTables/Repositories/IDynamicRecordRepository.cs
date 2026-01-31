using Atlas.Application.DynamicTables.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;

namespace Atlas.Application.DynamicTables.Repositories;

public interface IDynamicRecordRepository
{
    Task<long> InsertAsync(
        TenantId tenantId,
        DynamicTable table,
        IReadOnlyList<DynamicField> fields,
        DynamicRecordUpsertRequest request,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        TenantId tenantId,
        DynamicTable table,
        IReadOnlyList<DynamicField> fields,
        long id,
        DynamicRecordUpsertRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        DynamicTable table,
        IReadOnlyList<DynamicField> fields,
        long id,
        CancellationToken cancellationToken);

    Task DeleteBatchAsync(
        TenantId tenantId,
        DynamicTable table,
        IReadOnlyList<DynamicField> fields,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken);

    Task<DynamicRecordListResult> QueryAsync(
        TenantId tenantId,
        DynamicTable table,
        IReadOnlyList<DynamicField> fields,
        DynamicRecordQueryRequest request,
        CancellationToken cancellationToken);

    Task<DynamicRecordDto?> GetByIdAsync(
        TenantId tenantId,
        DynamicTable table,
        IReadOnlyList<DynamicField> fields,
        long id,
        CancellationToken cancellationToken);
}

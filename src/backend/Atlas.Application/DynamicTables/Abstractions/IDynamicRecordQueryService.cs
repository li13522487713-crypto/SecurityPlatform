using Atlas.Application.DynamicTables.Models;
using Atlas.Core.Tenancy;
using System.IO;

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

    Task<DynamicRecordExportResult> ExportAsync(
        TenantId tenantId,
        string tableKey,
        DynamicRecordExportRequest request,
        CancellationToken cancellationToken);

    Task<string> WriteCsvAsync(
        TenantId tenantId,
        string tableKey,
        DynamicRecordExportRequest request,
        Stream output,
        CancellationToken cancellationToken);

    Task<DynamicRecordListResult> GetRelatedRecordsAsync(
        TenantId tenantId,
        string sourceTableKey,
        long sourceRecordId,
        string relatedTableKey,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);
}

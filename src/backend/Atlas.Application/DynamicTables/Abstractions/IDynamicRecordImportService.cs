using Atlas.Application.DynamicTables.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.DynamicTables.Abstractions;

public interface IDynamicRecordImportService
{
    Task<DynamicRecordImportAnalyzeResult> AnalyzeAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        DynamicRecordImportRequest request,
        CancellationToken cancellationToken);

    Task<DynamicRecordImportResult> CommitAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        DynamicRecordImportCommitRequest request,
        CancellationToken cancellationToken);

    Task<DynamicRecordImportResult> ImportAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        DynamicRecordImportRequest request,
        CancellationToken cancellationToken);

    Task<DynamicRecordImportResult> PasteFromExcelAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        DynamicRecordImportRequest request,
        CancellationToken cancellationToken);
}

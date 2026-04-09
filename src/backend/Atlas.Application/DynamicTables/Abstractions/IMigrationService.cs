using Atlas.Application.DynamicTables.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.DynamicTables.Abstractions;

public interface IMigrationService
{
    Task ApplyDynamicAlterAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        DynamicTableAlterRequest request,
        CancellationToken cancellationToken);

    Task<DynamicTableAlterPreviewResponse> PreviewDynamicAlterAsync(
        TenantId tenantId,
        string tableKey,
        DynamicTableAlterRequest request,
        CancellationToken cancellationToken);

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

    Task<MigrationScriptPreview> DetectChangesAsync(
        TenantId tenantId,
        string tableKey,
        DynamicTableAlterRequest request,
        CancellationToken cancellationToken);

    Task<MigrationExecutionResult> ExecuteAsync(
        TenantId tenantId,
        long userId,
        long migrationId,
        bool confirmDestructive,
        CancellationToken cancellationToken);

    Task<MigrationPrecheckResult> PrecheckAsync(
        TenantId tenantId,
        long migrationId,
        CancellationToken cancellationToken);
}

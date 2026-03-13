using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IAiDatabaseService
{
    Task<PagedResult<AiDatabaseListItem>> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<AiDatabaseDetail?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<long> CreateAsync(
        TenantId tenantId,
        AiDatabaseCreateRequest request,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        TenantId tenantId,
        long id,
        AiDatabaseUpdateRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task BindAsync(
        TenantId tenantId,
        long id,
        long botId,
        CancellationToken cancellationToken);

    Task UnbindAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<PagedResult<AiDatabaseRecordListItem>> GetRecordsAsync(
        TenantId tenantId,
        long databaseId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<long> CreateRecordAsync(
        TenantId tenantId,
        long databaseId,
        AiDatabaseRecordCreateRequest request,
        CancellationToken cancellationToken);

    Task UpdateRecordAsync(
        TenantId tenantId,
        long databaseId,
        long recordId,
        AiDatabaseRecordUpdateRequest request,
        CancellationToken cancellationToken);

    Task DeleteRecordAsync(
        TenantId tenantId,
        long databaseId,
        long recordId,
        CancellationToken cancellationToken);

    Task<string> GetSchemaAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<AiDatabaseSchemaValidateResult> ValidateSchemaAsync(
        string schemaJson,
        CancellationToken cancellationToken);

    Task<long> SubmitImportAsync(
        TenantId tenantId,
        long databaseId,
        AiDatabaseImportRequest request,
        CancellationToken cancellationToken);

    Task<AiDatabaseImportProgress?> GetImportProgressAsync(
        TenantId tenantId,
        long databaseId,
        CancellationToken cancellationToken);

    Task<AiDatabaseTemplate> GetTemplateAsync(
        TenantId tenantId,
        long databaseId,
        CancellationToken cancellationToken);
}

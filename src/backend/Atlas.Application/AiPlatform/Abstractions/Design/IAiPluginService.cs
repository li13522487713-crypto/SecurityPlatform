using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IAiPluginService
{
    Task<PagedResult<AiPluginListItem>> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        long? workspaceId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<AiPluginDetail?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<long> CreateAsync(
        TenantId tenantId,
        AiPluginCreateRequest request,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        TenantId tenantId,
        long id,
        AiPluginUpdateRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task PublishAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task SetLockAsync(
        TenantId tenantId,
        long id,
        bool isLocked,
        CancellationToken cancellationToken);

    Task<AiPluginDebugResult> DebugAsync(
        TenantId tenantId,
        long id,
        AiPluginDebugRequest request,
        CancellationToken cancellationToken);

    Task<AiPluginOpenApiImportResult> ImportOpenApiAsync(
        TenantId tenantId,
        long id,
        AiPluginOpenApiImportRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AiPluginBuiltInMetaItem>> GetBuiltInMetadataAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<AiPluginApiItem>> GetApisAsync(
        TenantId tenantId,
        long pluginId,
        CancellationToken cancellationToken);

    Task<long> CreateApiAsync(
        TenantId tenantId,
        long pluginId,
        AiPluginApiCreateRequest request,
        CancellationToken cancellationToken);

    Task UpdateApiAsync(
        TenantId tenantId,
        long pluginId,
        long apiId,
        AiPluginApiUpdateRequest request,
        CancellationToken cancellationToken);

    Task DeleteApiAsync(
        TenantId tenantId,
        long pluginId,
        long apiId,
        CancellationToken cancellationToken);
}

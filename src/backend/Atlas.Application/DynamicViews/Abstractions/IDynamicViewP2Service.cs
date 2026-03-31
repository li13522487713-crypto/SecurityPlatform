using Atlas.Application.DynamicViews.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.DynamicViews.Abstractions;

public interface IDynamicViewP2Service
{
    Task<IReadOnlyList<DynamicExternalExtractDataSourceDto>> ListExternalExtractDataSourcesAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken);

    Task<DynamicExternalExtractSchemaResult> GetExternalExtractSchemaAsync(
        TenantId tenantId,
        long appId,
        long dataSourceId,
        CancellationToken cancellationToken);

    Task<DynamicExternalExtractPreviewResult> PreviewExternalExtractAsync(
        TenantId tenantId,
        long appId,
        long userId,
        DynamicExternalExtractPreviewRequest request,
        CancellationToken cancellationToken);

    Task<DynamicPhysicalViewPublishResult> PublishPhysicalViewAsync(
        TenantId tenantId,
        long userId,
        long? appId,
        string viewKey,
        DynamicPhysicalViewPublishRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DynamicPhysicalViewPublicationDto>> ListPhysicalPublicationsAsync(
        TenantId tenantId,
        long? appId,
        string viewKey,
        CancellationToken cancellationToken);

    Task<DynamicPhysicalViewPublishResult> RollbackPhysicalPublicationAsync(
        TenantId tenantId,
        long userId,
        long? appId,
        string viewKey,
        int version,
        CancellationToken cancellationToken);

    Task DeletePhysicalPublicationAsync(
        TenantId tenantId,
        long userId,
        long? appId,
        string viewKey,
        string publicationId,
        CancellationToken cancellationToken);
}

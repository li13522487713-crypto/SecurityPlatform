using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IAiAppService
{
    Task<PagedResult<AiAppListItem>> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<AiAppDetail?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<long> CreateAsync(
        TenantId tenantId,
        AiAppCreateRequest request,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        TenantId tenantId,
        long id,
        AiAppUpdateRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task PublishAsync(
        TenantId tenantId,
        long id,
        long publisherUserId,
        AiAppPublishRequest request,
        CancellationToken cancellationToken);

    Task<AiAppVersionCheckResult> CheckVersionAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<long> SubmitResourceCopyAsync(
        TenantId tenantId,
        long appId,
        AiAppResourceCopyRequest request,
        CancellationToken cancellationToken);

    Task<AiAppResourceCopyTaskProgress?> GetLatestResourceCopyProgressAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken);
}

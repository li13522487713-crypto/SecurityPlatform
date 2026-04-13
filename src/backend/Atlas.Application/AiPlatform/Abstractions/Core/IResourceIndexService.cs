using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions.Core;

public interface IResourceIndexService
{
    Task<PagedResult<ResourceIndexItem>> GetWorkspaceResourcesAsync(
        TenantId tenantId,
        long userId,
        string? keyword,
        string? resourceType,
        bool favoriteOnly,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<AiLibraryPagedResult> GetLibraryResourcesAsync(
        TenantId tenantId,
        AiLibraryQueryRequest request,
        CancellationToken cancellationToken);
}

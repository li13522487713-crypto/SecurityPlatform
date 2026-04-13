using Atlas.Application.Platform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Platform.Abstractions;

public interface IWorkspaceIdeService
{
    Task<WorkspaceIdeSummaryResponse> GetSummaryAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<WorkspaceIdeResourceCardResponse>> GetResourcesAsync(
        TenantId tenantId,
        long userId,
        WorkspaceIdeResourceQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkspaceIdeCreateAppResult> CreateAppAsync(
        TenantId tenantId,
        long userId,
        WorkspaceIdeCreateAppRequest request,
        CancellationToken cancellationToken = default);

    Task UpdateFavoriteAsync(
        TenantId tenantId,
        long userId,
        string resourceType,
        long resourceId,
        WorkspaceIdeFavoriteUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task RecordActivityAsync(
        TenantId tenantId,
        long userId,
        WorkspaceIdeActivityCreateRequest request,
        CancellationToken cancellationToken = default);
}

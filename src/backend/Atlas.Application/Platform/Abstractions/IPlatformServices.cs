using Atlas.Application.Platform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Platform.Abstractions;

public interface IPlatformQueryService
{
    Task<PlatformOverviewResponse> GetOverviewAsync(TenantId tenantId, CancellationToken cancellationToken = default);
    Task<PlatformResourcesResponse> GetResourcesAsync(TenantId tenantId, CancellationToken cancellationToken = default);
    Task<PagedResult<AppReleaseResponse>> GetReleasesAsync(TenantId tenantId, PagedRequest request, CancellationToken cancellationToken = default);
}

public interface IAppManifestQueryService
{
    Task<PagedResult<AppManifestResponse>> QueryAsync(TenantId tenantId, PagedRequest request, CancellationToken cancellationToken = default);
    Task<AppManifestResponse?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default);
    Task<WorkspaceOverviewResponse> GetWorkspaceOverviewAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default);
    Task<PagedResult<object>> GetWorkspacePagesAsync(TenantId tenantId, long id, PagedRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<object>> GetWorkspaceFormsAsync(TenantId tenantId, long id, PagedRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<object>> GetWorkspaceFlowsAsync(TenantId tenantId, long id, PagedRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<object>> GetWorkspaceDataAsync(TenantId tenantId, long id, PagedRequest request, CancellationToken cancellationToken = default);
    Task<WorkspacePermissionResponse> GetWorkspacePermissionsAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default);
}

public interface IAppManifestCommandService
{
    Task<long> CreateAsync(TenantId tenantId, long userId, AppManifestCreateRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(TenantId tenantId, long userId, long id, AppManifestUpdateRequest request, CancellationToken cancellationToken = default);
    Task ArchiveAsync(TenantId tenantId, long userId, long id, CancellationToken cancellationToken = default);
}

public interface IAppReleaseCommandService
{
    Task<long> CreateReleaseAsync(TenantId tenantId, long userId, long manifestId, string? releaseNote, CancellationToken cancellationToken = default);
    Task RollbackAsync(TenantId tenantId, long userId, long manifestId, long releaseId, CancellationToken cancellationToken = default);
}

public interface IRuntimeRouteQueryService
{
    Task<RuntimePageResponse?> GetRuntimePageAsync(TenantId tenantId, string appKey, string pageKey, CancellationToken cancellationToken = default);
    Task<PagedResult<RuntimeTaskListItem>> GetRuntimeTasksAsync(TenantId tenantId, long userId, PagedRequest request, CancellationToken cancellationToken = default);
    Task<RuntimeMenuResponse> GetRuntimeMenuAsync(TenantId tenantId, string appKey, CancellationToken cancellationToken = default);
    Task<bool> ExecuteRuntimeTaskActionAsync(TenantId tenantId, long userId, long taskId, RuntimeTaskActionRequest request, CancellationToken cancellationToken = default);
}

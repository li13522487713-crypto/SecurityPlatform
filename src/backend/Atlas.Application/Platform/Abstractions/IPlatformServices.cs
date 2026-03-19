using Atlas.Application.Platform.Models;
using Atlas.Application.LowCode.Models;
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
    Task<PagedResult<RuntimeTaskListItem>> GetRuntimeDoneTasksAsync(TenantId tenantId, long userId, PagedRequest request, CancellationToken cancellationToken = default);
    Task<RuntimeMenuResponse> GetRuntimeMenuAsync(TenantId tenantId, string appKey, CancellationToken cancellationToken = default);
    Task<bool> ExecuteRuntimeTaskActionAsync(TenantId tenantId, long userId, long taskId, RuntimeTaskActionRequest request, CancellationToken cancellationToken = default);
}

public interface IApplicationCatalogQueryService
{
    Task<PagedResult<ApplicationCatalogListItem>> QueryAsync(TenantId tenantId, PagedRequest request, CancellationToken cancellationToken = default);
    Task<ApplicationCatalogDetail?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default);
}

public interface ITenantApplicationQueryService
{
    Task<PagedResult<TenantApplicationListItem>> QueryAsync(
        TenantId tenantId,
        PagedRequest request,
        CancellationToken cancellationToken = default);

    Task<TenantApplicationDetail?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken = default);
}

public interface ITenantAppInstanceQueryService
{
    Task<PagedResult<TenantAppInstanceListItem>> QueryAsync(TenantId tenantId, PagedRequest request, CancellationToken cancellationToken = default);
    Task<TenantAppInstanceDetail?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantAppDataSourceBinding>> GetDataSourceBindingsAsync(
        TenantId tenantId,
        IReadOnlyCollection<long>? appInstanceIds,
        CancellationToken cancellationToken = default);
}

public interface ITenantAppInstanceCommandService
{
    Task<long> CreateAsync(
        TenantId tenantId,
        long userId,
        LowCodeAppCreateRequest request,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        TenantId tenantId,
        long userId,
        long id,
        LowCodeAppUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task PublishAsync(
        TenantId tenantId,
        long userId,
        long id,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        TenantId tenantId,
        long userId,
        long id,
        CancellationToken cancellationToken = default);

    Task<LowCodeAppExportPackage?> ExportAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken = default);

    Task<LowCodeAppImportResult> ImportAsync(
        TenantId tenantId,
        long userId,
        LowCodeAppImportRequest request,
        CancellationToken cancellationToken = default);
}

public interface ITenantAppMemberQueryService
{
    Task<PagedResult<TenantAppMemberListItem>> QueryAsync(
        TenantId tenantId,
        long appId,
        PagedRequest request,
        CancellationToken cancellationToken = default);

    Task<TenantAppMemberDetail?> GetByUserIdAsync(
        TenantId tenantId,
        long appId,
        long userId,
        CancellationToken cancellationToken = default);
}

public interface ITenantAppMemberCommandService
{
    Task AddMembersAsync(
        TenantId tenantId,
        long appId,
        long operatorUserId,
        TenantAppMemberAssignRequest request,
        CancellationToken cancellationToken = default);

    Task UpdateMemberRolesAsync(
        TenantId tenantId,
        long appId,
        long userId,
        TenantAppMemberUpdateRolesRequest request,
        CancellationToken cancellationToken = default);

    Task RemoveMemberAsync(
        TenantId tenantId,
        long appId,
        long userId,
        CancellationToken cancellationToken = default);
}

public interface ITenantAppRoleQueryService
{
    Task<PagedResult<TenantAppRoleListItem>> QueryAsync(
        TenantId tenantId,
        long appId,
        PagedRequest request,
        CancellationToken cancellationToken = default);

    Task<TenantAppRoleDetail?> GetByIdAsync(
        TenantId tenantId,
        long appId,
        long roleId,
        CancellationToken cancellationToken = default);
}

public interface ITenantAppRoleCommandService
{
    Task<long> CreateAsync(
        TenantId tenantId,
        long appId,
        long operatorUserId,
        TenantAppRoleCreateRequest request,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        TenantId tenantId,
        long appId,
        long roleId,
        long operatorUserId,
        TenantAppRoleUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task UpdatePermissionsAsync(
        TenantId tenantId,
        long appId,
        long roleId,
        TenantAppRoleAssignPermissionsRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        TenantId tenantId,
        long appId,
        long roleId,
        CancellationToken cancellationToken = default);
}

public interface IRuntimeContextQueryService
{
    Task<RuntimeContextDetail?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken = default);

    Task<PagedResult<RuntimeContextListItem>> QueryAsync(
        TenantId tenantId,
        PagedRequest request,
        string? appKey = null,
        string? pageKey = null,
        CancellationToken cancellationToken = default);

    Task<RuntimeContextDetail?> GetByRouteAsync(
        TenantId tenantId,
        string appKey,
        string pageKey,
        CancellationToken cancellationToken = default);
}

public interface IRuntimeExecutionQueryService
{
    Task<PagedResult<RuntimeExecutionListItem>> QueryAsync(
        TenantId tenantId,
        PagedRequest request,
        CancellationToken cancellationToken = default);

    Task<RuntimeExecutionDetail?> GetByIdAsync(
        TenantId tenantId,
        long executionId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<RuntimeExecutionAuditTrailItem>> GetAuditTrailsAsync(
        TenantId tenantId,
        long executionId,
        PagedRequest request,
        CancellationToken cancellationToken = default);
}

public interface IResourceCenterQueryService
{
    Task<IReadOnlyList<ResourceCenterGroupItem>> GetGroupsAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default);

    Task<ResourceCenterDataSourceConsumptionResponse> GetDataSourceConsumptionAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default);
}

public interface IReleaseCenterQueryService
{
    Task<PagedResult<ReleaseCenterListItem>> QueryAsync(
        TenantId tenantId,
        PagedRequest request,
        CancellationToken cancellationToken = default);

    Task<ReleaseCenterDetail?> GetByIdAsync(
        TenantId tenantId,
        long releaseId,
        CancellationToken cancellationToken = default);
}

public interface ICozeMappingQueryService
{
    Task<CozeLayerMappingOverview> GetOverviewAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default);
}

public interface IDebugLayerQueryService
{
    Task<DebugLayerEmbedMetadata> GetEmbedMetadataAsync(
        TenantId tenantId,
        long userId,
        string appId,
        long? projectId,
        bool projectScopeEnabled,
        CancellationToken cancellationToken = default);
}

using Atlas.Application.Platform.Models;
using Atlas.Application.LowCode.Models;
using Atlas.Application.System.Models;
using Atlas.Application.Identity.Models;
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
    Task<PagedResult<AppManifestResponse>> QueryAsync(
        TenantId tenantId,
        PagedRequest request,
        string? status = null,
        string? category = null,
        string? appKey = null,
        CancellationToken cancellationToken = default);
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

/// <summary>
/// 应用 Bootstrap 服务：创建应用时自动播种默认角色/权限/组织根节点，
/// 确保新应用无需手动配置即可投入使用。
/// </summary>
public interface IAppBootstrapService
{
    Task BootstrapAsync(TenantId tenantId, long appId, long creatorUserId, CancellationToken cancellationToken = default);
}

public interface IAppReleaseCommandService
{
    Task<long> CreateReleaseAsync(TenantId tenantId, long userId, long manifestId, string? releaseNote, CancellationToken cancellationToken = default);
    Task<ReleaseRollbackResult> RollbackAsync(TenantId tenantId, long userId, long manifestId, long releaseId, CancellationToken cancellationToken = default);
    Task<ReleasePreCheckResult> PreCheckAsync(TenantId tenantId, long manifestId, CancellationToken cancellationToken = default);
}

public interface IAppDesignerSnapshotService
{
    Task<DesignerSnapshotResponse?> GetSnapshotAsync(TenantId tenantId, long manifestId, string type, long itemId, CancellationToken cancellationToken = default);
    Task SaveSnapshotAsync(TenantId tenantId, long userId, long manifestId, string type, long itemId, string schemaJson, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DesignerSnapshotHistoryItem>> GetSnapshotHistoryAsync(TenantId tenantId, long manifestId, string type, long itemId, CancellationToken cancellationToken = default);
}

public interface IRuntimeRouteQueryService
{
    Task<RuntimePageResponse?> GetRuntimePageAsync(TenantId tenantId, string appKey, string pageKey, CancellationToken cancellationToken = default);
    Task<RuntimePageResponse?> GetRuntimePageAsync(TenantId tenantId, long appId, string appKey, string pageKey, CancellationToken cancellationToken = default);
    Task<PagedResult<RuntimeTaskListItem>> GetRuntimeTasksAsync(TenantId tenantId, long userId, PagedRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<RuntimeTaskListItem>> GetRuntimeDoneTasksAsync(TenantId tenantId, long userId, PagedRequest request, CancellationToken cancellationToken = default);
    Task<RuntimeMenuResponse> GetRuntimeMenuAsync(TenantId tenantId, string appKey, CancellationToken cancellationToken = default);
    Task<bool> ExecuteRuntimeTaskActionAsync(TenantId tenantId, long userId, long taskId, RuntimeTaskActionRequest request, CancellationToken cancellationToken = default);
}

public interface IApplicationCatalogQueryService
{
    Task<PagedResult<ApplicationCatalogListItem>> QueryAsync(
        TenantId tenantId,
        PagedRequest request,
        string? status = null,
        string? category = null,
        string? appKey = null,
        CancellationToken cancellationToken = default);
    Task<ApplicationCatalogDetail?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default);
}

public interface IApplicationCatalogCommandService
{
    Task UpdateAsync(
        TenantId tenantId,
        long userId,
        long id,
        ApplicationCatalogUpdateRequest request,
        CancellationToken cancellationToken = default);
    Task PublishAsync(
        TenantId tenantId,
        long userId,
        long id,
        ApplicationCatalogPublishRequest request,
        CancellationToken cancellationToken = default);
    Task UpdateDataSourceAsync(
        TenantId tenantId,
        long userId,
        long id,
        ApplicationCatalogDataSourceUpdateRequest request,
        CancellationToken cancellationToken = default);
}

public interface ITenantApplicationQueryService
{
    Task<PagedResult<TenantApplicationListItem>> QueryAsync(
        TenantId tenantId,
        PagedRequest request,
        string? status = null,
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
    Task<TenantAppInstanceRuntimeInfo?> GetRuntimeInfoAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default);
    Task<TenantAppInstanceHealthInfo?> GetHealthAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LowCodeAppEntityAliasItem>> GetEntityAliasesAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default);
    Task<LowCodeAppDataSourceInfo?> GetDataSourceInfoAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default);
    Task<TestConnectionResult> TestDataSourceAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default);
    Task<TenantAppFileStorageSettings?> GetFileStorageSettingsAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantAppDataSourceBinding>> GetDataSourceBindingsAsync(
        TenantId tenantId,
        IReadOnlyCollection<long>? appInstanceIds,
        CancellationToken cancellationToken = default);
}

public interface ITenantAppInstanceCommandService
{
    Task<TenantAppInstanceRuntimeInfo> StartAsync(
        TenantId tenantId,
        long userId,
        long id,
        CancellationToken cancellationToken = default);

    Task<TenantAppInstanceRuntimeInfo> StopAsync(
        TenantId tenantId,
        long userId,
        long id,
        CancellationToken cancellationToken = default);

    Task<TenantAppInstanceRuntimeInfo> RestartAsync(
        TenantId tenantId,
        long userId,
        long id,
        CancellationToken cancellationToken = default);

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

    Task UpdateEntityAliasesAsync(
        TenantId tenantId,
        long userId,
        long id,
        LowCodeAppEntityAliasesUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task UpdateFileStorageSettingsAsync(
        TenantId tenantId,
        long userId,
        long id,
        TenantAppFileStorageSettingsUpdateRequest request,
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

public interface IAppInstanceRegistry
{
    Task<TenantAppInstanceRuntimeInfo> EnsureAsync(
        TenantId tenantId,
        TenantAppInstanceRuntimeRegistration registration,
        CancellationToken cancellationToken = default);

    Task<TenantAppInstanceRuntimeInfo?> GetAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<long, TenantAppInstanceRuntimeInfo>> GetManyAsync(
        TenantId tenantId,
        IReadOnlyCollection<long> appInstanceIds,
        CancellationToken cancellationToken = default);

    Task<TenantAppInstanceRuntimeInfo> SaveAsync(
        TenantId tenantId,
        TenantAppInstanceRuntimeInfo runtimeInfo,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(TenantId TenantId, TenantAppInstanceRuntimeInfo RuntimeInfo)>> GetAllRunningAsync(
        CancellationToken cancellationToken = default);
}

public interface IAppProcessManager
{
    Task<TenantAppInstanceRuntimeInfo> StartAsync(
        TenantAppInstanceRuntimeInfo runtimeInfo,
        CancellationToken cancellationToken = default);

    Task<TenantAppInstanceRuntimeInfo> StopAsync(
        TenantAppInstanceRuntimeInfo runtimeInfo,
        CancellationToken cancellationToken = default);
}

public interface IAppHealthProbe
{
    Task<TenantAppInstanceHealthInfo> ProbeAsync(
        TenantAppInstanceRuntimeInfo runtimeInfo,
        CancellationToken cancellationToken = default);
}

public interface IAppIngressResolver
{
    string ResolveIngressUrl(TenantAppInstanceRuntimeInfo runtimeInfo);
}

public interface IAppLoginEntryResolver
{
    string ResolveLoginUrl(TenantAppInstanceRuntimeInfo runtimeInfo);
}

public interface IAppRuntimeSupervisor
{
    Task<IReadOnlyDictionary<long, TenantAppInstanceRuntimeInfo>> GetRuntimeSnapshotMapAsync(
        TenantId tenantId,
        IReadOnlyCollection<long> appInstanceIds,
        CancellationToken cancellationToken = default);

    Task<TenantAppInstanceRuntimeInfo?> GetRuntimeInfoAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken = default);

    Task<TenantAppInstanceHealthInfo?> GetHealthAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken = default);

    Task<TenantAppInstanceRuntimeInfo> StartAsync(
        TenantId tenantId,
        TenantAppInstanceRuntimeRegistration registration,
        CancellationToken cancellationToken = default);

    Task<TenantAppInstanceRuntimeInfo> StopAsync(
        TenantId tenantId,
        TenantAppInstanceRuntimeRegistration registration,
        CancellationToken cancellationToken = default);

    Task<TenantAppInstanceRuntimeInfo> RestartAsync(
        TenantId tenantId,
        TenantAppInstanceRuntimeRegistration registration,
        CancellationToken cancellationToken = default);
}

public interface ITenantAppMemberQueryService
{
    Task<PagedResult<TenantAppMemberListItem>> QueryAsync(
        TenantId tenantId,
        long appId,
        PagedRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<TenantAppMemberListItem>> QueryByRoleAsync(
        TenantId tenantId,
        long appId,
        long roleId,
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

    Task<TenantAppRoleGovernanceOverview> GetGovernanceOverviewAsync(
        TenantId tenantId,
        long appId,
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
        string? appId = null,
        string? status = null,
        DateTimeOffset? startedFrom = null,
        DateTimeOffset? startedTo = null,
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

    Task<RuntimeExecutionStats> GetStatsAsync(
        TenantId tenantId,
        string? appId = null,
        DateTimeOffset? startedFrom = null,
        DateTimeOffset? startedTo = null,
        CancellationToken cancellationToken = default);
}

public interface IRuntimeExecutionCommandService
{
    Task<RuntimeExecutionOperationResult> CancelAsync(
        TenantId tenantId,
        long operatorUserId,
        long executionId,
        CancellationToken cancellationToken = default);

    Task<RuntimeExecutionOperationResult> RetryAsync(
        TenantId tenantId,
        long operatorUserId,
        long executionId,
        CancellationToken cancellationToken = default);

    Task<RuntimeExecutionOperationResult> ResumeAsync(
        TenantId tenantId,
        long operatorUserId,
        long executionId,
        CancellationToken cancellationToken = default);

    Task<RuntimeExecutionOperationResult> DebugAsync(
        TenantId tenantId,
        long operatorUserId,
        long executionId,
        RuntimeExecutionDebugRequest request,
        CancellationToken cancellationToken = default);

    Task<RuntimeExecutionTimeoutDiagnosis?> GetTimeoutDiagnosisAsync(
        TenantId tenantId,
        long executionId,
        CancellationToken cancellationToken = default);
}

public interface IResourceCenterQueryService
{
    Task<ResourceCenterGroupsResponse> GetGroupsAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default);

    Task<ResourceCenterGroupsSummaryResponse> GetGroupsSummaryAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default);

    Task<ResourceCenterDataSourceConsumptionResponse> GetDataSourceConsumptionAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default);

    Task<ResourceCenterDataSourceConsumptionSummaryResponse> GetDataSourceConsumptionSummaryAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default);
}

public interface IResourceCenterCommandService
{
    Task<ResourceCenterRepairResult> DisableInvalidBindingAsync(
        TenantId tenantId,
        long operatorUserId,
        DisableInvalidBindingRequest request,
        CancellationToken cancellationToken = default);

    Task<ResourceCenterRepairResult> SwitchPrimaryBindingAsync(
        TenantId tenantId,
        long operatorUserId,
        SwitchPrimaryBindingRequest request,
        CancellationToken cancellationToken = default);

    Task<ResourceCenterRepairResult> UnbindOrphanBindingAsync(
        TenantId tenantId,
        long operatorUserId,
        UnbindOrphanBindingRequest request,
        CancellationToken cancellationToken = default);
}

public interface IReleaseCenterQueryService
{
    Task<PagedResult<ReleaseCenterListItem>> QueryAsync(
        TenantId tenantId,
        PagedRequest request,
        string? status = null,
        string? appKey = null,
        long? manifestId = null,
        CancellationToken cancellationToken = default);

    Task<ReleaseCenterDetail?> GetByIdAsync(
        TenantId tenantId,
        long releaseId,
        CancellationToken cancellationToken = default);

    Task<ReleaseDiffSummary?> GetDiffAsync(
        TenantId tenantId,
        long releaseId,
        CancellationToken cancellationToken = default);

    Task<ReleaseImpactSummary?> GetImpactAsync(
        TenantId tenantId,
        long releaseId,
        CancellationToken cancellationToken = default);

    Task<ReleaseInstallStatusInfo?> GetInstallStatusAsync(
        TenantId tenantId,
        long releaseId,
        long tenantAppInstanceId,
        CancellationToken cancellationToken = default);
}

public interface IAppPackageBuilder
{
    Task<AppPackageBuildResult> BuildAsync(
        TenantId tenantId,
        long releaseId,
        CancellationToken cancellationToken = default);
}

public interface IAppPackageInstaller
{
    Task<ReleaseInstallResult> InstallAsync(
        TenantId tenantId,
        long releaseId,
        long tenantAppInstanceId,
        CancellationToken cancellationToken = default);

    Task<ReleaseInstallResult> RollbackAsync(
        TenantId tenantId,
        long releaseId,
        long tenantAppInstanceId,
        CancellationToken cancellationToken = default);
}

public interface IAppReleaseOrchestrator
{
    Task<ReleaseInstallResult> InstallAsync(
        TenantId tenantId,
        long releaseId,
        long tenantAppInstanceId,
        CancellationToken cancellationToken = default);

    Task<ReleaseInstallResult> RollbackAsync(
        TenantId tenantId,
        long releaseId,
        long tenantAppInstanceId,
        CancellationToken cancellationToken = default);

    Task<ReleaseInstallStatusInfo?> GetInstallStatusAsync(
        TenantId tenantId,
        long releaseId,
        long tenantAppInstanceId,
        CancellationToken cancellationToken = default);
}

public interface IAppEntryQueryService
{
    Task<AppEntryInfo?> GetEntryAsync(
        TenantId tenantId,
        string appKey,
        CancellationToken cancellationToken = default);

    Task<AppEntryLoginBeginResult?> BeginLoginAsync(
        TenantId tenantId,
        string appKey,
        string? redirectUri,
        CancellationToken cancellationToken = default);

    Task<AppEntryLoginOptions?> GetLoginOptionsAsync(
        TenantId tenantId,
        string appKey,
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

public interface IAppOrgQueryService
{
    // Departments
    Task<PagedResult<AppDepartmentListItem>> QueryDepartmentsAsync(TenantId tenantId, long appId, PagedRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AppDepartmentListItem>> GetAllDepartmentsAsync(TenantId tenantId, long appId, CancellationToken cancellationToken = default);
    Task<AppDepartmentDetail?> GetDepartmentByIdAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default);
    // Positions
    Task<PagedResult<AppPositionListItem>> QueryPositionsAsync(TenantId tenantId, long appId, PagedRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AppPositionListItem>> GetAllPositionsAsync(TenantId tenantId, long appId, CancellationToken cancellationToken = default);
    Task<AppPositionDetail?> GetPositionByIdAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default);
    // Projects
    Task<PagedResult<AppProjectListItem>> QueryProjectsAsync(TenantId tenantId, long appId, PagedRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AppProjectListItem>> GetAllProjectsAsync(TenantId tenantId, long appId, CancellationToken cancellationToken = default);
    Task<AppProjectDetail?> GetProjectByIdAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default);
}

public interface IAppOrgCommandService
{
    // Departments
    Task<long> CreateDepartmentAsync(TenantId tenantId, long appId, AppDepartmentCreateRequest request, CancellationToken cancellationToken = default);
    Task UpdateDepartmentAsync(TenantId tenantId, long appId, long id, AppDepartmentUpdateRequest request, CancellationToken cancellationToken = default);
    Task DeleteDepartmentAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default);
    // Positions
    Task<long> CreatePositionAsync(TenantId tenantId, long appId, AppPositionCreateRequest request, CancellationToken cancellationToken = default);
    Task UpdatePositionAsync(TenantId tenantId, long appId, long id, AppPositionUpdateRequest request, CancellationToken cancellationToken = default);
    Task DeletePositionAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default);
    // Projects
    Task<long> CreateProjectAsync(TenantId tenantId, long appId, AppProjectCreateRequest request, CancellationToken cancellationToken = default);
    Task UpdateProjectAsync(TenantId tenantId, long appId, long id, AppProjectUpdateRequest request, CancellationToken cancellationToken = default);
    Task DeleteProjectAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default);
}

public interface IAppRoleAssignmentQueryService
{
    Task<AppRoleAssignmentDetail> GetRoleAssignmentAsync(TenantId tenantId, long appId, long roleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<long>> GetRolePagesAsync(TenantId tenantId, long appId, long roleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AppRoleFieldPermissionGroup>> GetRoleFieldPermissionsAsync(TenantId tenantId, long appId, long roleId, CancellationToken cancellationToken = default);
}

public interface IAppRoleAssignmentCommandService
{
    Task SetDataScopeAsync(TenantId tenantId, long appId, long roleId, AppRoleDataScopeRequest request, CancellationToken cancellationToken = default);
    Task SetRolePagesAsync(TenantId tenantId, long appId, long roleId, IReadOnlyList<long> pageIds, CancellationToken cancellationToken = default);
    Task SetRoleFieldPermissionsAsync(TenantId tenantId, long appId, long roleId, AppRoleFieldPermissionsRequest request, CancellationToken cancellationToken = default);
}

public interface IAppPermissionQueryService
{
    Task<PagedResult<PermissionListItem>> QueryAsync(TenantId tenantId, long appId, PermissionQueryRequest request, CancellationToken cancellationToken = default);
    Task<PermissionDetail?> GetByIdAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default);
}

public interface IAppPermissionCommandService
{
    Task<long> CreateAsync(TenantId tenantId, long appId, PermissionCreateRequest request, long id, CancellationToken cancellationToken = default);
    Task UpdateAsync(TenantId tenantId, long appId, long id, PermissionUpdateRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default);
}

public interface IAppOrganizationQueryService
{
    Task<AppOrganizationWorkspaceResponse> GetWorkspaceAsync(
        TenantId tenantId,
        long appId,
        PagedRequest request,
        long? roleId = null,
        CancellationToken cancellationToken = default);
}

public interface IAppOrganizationCommandService
{
    Task<long> CreateMemberUserAsync(
        TenantId tenantId,
        long appId,
        AppOrganizationCreateMemberUserRequest request,
        CancellationToken cancellationToken = default);

    Task AddMembersAsync(
        TenantId tenantId,
        long appId,
        long operatorUserId,
        AppOrganizationAssignMembersRequest request,
        CancellationToken cancellationToken = default);

    Task UpdateMemberRolesAsync(
        TenantId tenantId,
        long appId,
        string userId,
        AppOrganizationUpdateMemberRolesRequest request,
        CancellationToken cancellationToken = default);

    Task ResetMemberPasswordAsync(
        TenantId tenantId,
        long appId,
        string userId,
        AppOrganizationResetMemberPasswordRequest request,
        CancellationToken cancellationToken = default);

    Task UpdateMemberProfileAsync(
        TenantId tenantId,
        long appId,
        string userId,
        AppOrganizationUpdateMemberProfileRequest request,
        CancellationToken cancellationToken = default);

    Task RemoveMemberAsync(
        TenantId tenantId,
        long appId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<long> CreateRoleAsync(
        TenantId tenantId,
        long appId,
        long operatorUserId,
        AppOrganizationCreateRoleRequest request,
        CancellationToken cancellationToken = default);

    Task UpdateRoleAsync(
        TenantId tenantId,
        long appId,
        string roleId,
        long operatorUserId,
        AppOrganizationUpdateRoleRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteRoleAsync(
        TenantId tenantId,
        long appId,
        string roleId,
        CancellationToken cancellationToken = default);

    Task<long> CreateDepartmentAsync(
        TenantId tenantId,
        long appId,
        AppOrganizationCreateDepartmentRequest request,
        CancellationToken cancellationToken = default);

    Task UpdateDepartmentAsync(
        TenantId tenantId,
        long appId,
        string id,
        AppOrganizationUpdateDepartmentRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteDepartmentAsync(
        TenantId tenantId,
        long appId,
        string id,
        CancellationToken cancellationToken = default);

    Task<long> CreatePositionAsync(
        TenantId tenantId,
        long appId,
        AppOrganizationCreatePositionRequest request,
        CancellationToken cancellationToken = default);

    Task UpdatePositionAsync(
        TenantId tenantId,
        long appId,
        string id,
        AppOrganizationUpdatePositionRequest request,
        CancellationToken cancellationToken = default);

    Task DeletePositionAsync(
        TenantId tenantId,
        long appId,
        string id,
        CancellationToken cancellationToken = default);

    Task<long> CreateProjectAsync(
        TenantId tenantId,
        long appId,
        AppOrganizationCreateProjectRequest request,
        CancellationToken cancellationToken = default);

    Task UpdateProjectAsync(
        TenantId tenantId,
        long appId,
        string id,
        AppOrganizationUpdateProjectRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteProjectAsync(
        TenantId tenantId,
        long appId,
        string id,
        CancellationToken cancellationToken = default);
}

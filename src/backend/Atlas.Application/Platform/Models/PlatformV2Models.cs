using Atlas.Core.Models;

namespace Atlas.Application.Platform.Models;

public sealed record ApplicationCatalogListItem(
    string Id,
    string CatalogKey,
    string Name,
    string Status,
    int Version,
    string? Description,
    string? Category,
    string? Icon,
    string? PublishedAt,
    bool IsBound);

public sealed record ApplicationCatalogDetail(
    string Id,
    string CatalogKey,
    string Name,
    string Status,
    int Version,
    string? Description,
    string? Category,
    string? Icon,
    string? PublishedAt,
    string? DataSourceId,
    bool IsBound);

public sealed record ApplicationCatalogDataSourceUpdateRequest(
    string DataSourceId);

public sealed record ApplicationCatalogUpdateRequest(
    string Name,
    string? Description,
    string? Category,
    string? Icon);

public sealed record ApplicationCatalogPublishRequest(
    string? ReleaseNote);

public sealed record TenantApplicationListItem(
    string Id,
    string ApplicationCatalogId,
    string ApplicationCatalogName,
    string TenantAppInstanceId,
    string AppKey,
    string Name,
    string Status,
    string OpenedAt,
    string? DataSourceId);

public sealed record TenantApplicationDetail(
    string Id,
    string ApplicationCatalogId,
    string ApplicationCatalogName,
    string TenantAppInstanceId,
    string AppKey,
    string Name,
    string Status,
    string OpenedAt,
    string UpdatedAt,
    string? DataSourceId);

public sealed record TenantAppInstanceListItem(
    string Id,
    string AppKey,
    string Name,
    string Status,
    int Version,
    string? Description,
    string? Category,
    string? Icon,
    string? PublishedAt,
    string? CurrentArtifactId,
    string? RuntimeStatus,
    string? HealthStatus,
    int? AssignedPort,
    int? CurrentPid,
    string? IngressUrl);

public sealed record TenantAppInstanceDetail(
    string Id,
    string AppKey,
    string Name,
    string Status,
    int Version,
    string? Description,
    string? Category,
    string? Icon,
    string? PublishedAt,
    string? DataSourceId,
    int PageCount,
    string? CurrentArtifactId,
    string? RuntimeStatus,
    string? HealthStatus,
    int? AssignedPort,
    int? CurrentPid,
    string? IngressUrl,
    string? LoginUrl,
    string? InstanceHome,
    string? LastStartedAt,
    string? LastHealthCheckedAt);

public sealed record TenantAppInstanceRuntimeRegistration(
    string AppInstanceId,
    string AppKey,
    string Name,
    int Version,
    string? CurrentArtifactId);

public sealed record TenantAppInstanceRuntimeInfo(
    string InstanceId,
    string AppKey,
    string RuntimeStatus,
    string HealthStatus,
    int? AssignedPort,
    int? CurrentPid,
    string? CurrentArtifactId,
    string? IngressUrl,
    string? LoginUrl,
    string? InstanceHome,
    string? ConfigPath,
    string? StartedAt,
    string? StoppedAt,
    string? LastHealthCheckedAt,
    int? LastExitCode = null,
    string? CurrentReleaseVersion = null);

public sealed record TenantAppInstanceHealthInfo(
    string InstanceId,
    string RuntimeStatus,
    string HealthStatus,
    bool Live,
    bool Ready,
    string? Version,
    string? Message,
    string CheckedAt,
    string? IngressUrl);

public sealed record TenantAppDataSourceBinding(
    string TenantAppInstanceId,
    string? DataSourceId,
    string? DataSourceName,
    string? DbType,
    bool? DataSourceActive,
    string? LastTestedAt,
    string? BindingId,
    string? BindingType,
    bool? BindingActive,
    string? BoundAt,
    string? Source);

public sealed record TenantAppFileStorageSettings(
    string TenantAppInstanceId,
    string AppId,
    string EffectiveBasePath,
    string EffectiveMinioBucketName,
    string? OverrideBasePath,
    string? OverrideMinioBucketName,
    bool InheritBasePath,
    bool InheritMinioBucketName);

public sealed record TenantAppFileStorageSettingsUpdateRequest(
    string? OverrideBasePath,
    string? OverrideMinioBucketName,
    bool InheritBasePath,
    bool InheritMinioBucketName);

public sealed record TenantAppMemberListItem(
    string UserId,
    string Username,
    string DisplayName,
    string? Email,
    string? PhoneNumber,
    bool IsActive,
    string JoinedAt,
    IReadOnlyList<string> RoleIds,
    IReadOnlyList<string> RoleNames,
    IReadOnlyList<string> DepartmentIds,
    IReadOnlyList<string> DepartmentNames,
    IReadOnlyList<string> PositionIds,
    IReadOnlyList<string> PositionNames,
    IReadOnlyList<string> ProjectIds,
    IReadOnlyList<string> ProjectNames);

public sealed record TenantAppMemberDetail(
    string UserId,
    string Username,
    string DisplayName,
    string? Email,
    string? PhoneNumber,
    bool IsActive,
    string JoinedAt,
    IReadOnlyList<string> RoleIds,
    IReadOnlyList<string> RoleNames,
    IReadOnlyList<string> DepartmentIds,
    IReadOnlyList<string> DepartmentNames,
    IReadOnlyList<string> PositionIds,
    IReadOnlyList<string> PositionNames,
    IReadOnlyList<string> ProjectIds,
    IReadOnlyList<string> ProjectNames);

public sealed record TenantAppMemberAssignRequest(
    IReadOnlyList<long> UserIds,
    IReadOnlyList<long> RoleIds,
    IReadOnlyList<long>? DepartmentIds,
    IReadOnlyList<long>? PositionIds,
    IReadOnlyList<long>? ProjectIds);

public sealed record TenantAppMemberUpdateRolesRequest(
    IReadOnlyList<long> RoleIds,
    IReadOnlyList<long>? DepartmentIds,
    IReadOnlyList<long>? PositionIds,
    IReadOnlyList<long>? ProjectIds);

public sealed record TenantAppRoleListItem(
    string Id,
    string Code,
    string Name,
    string? Description,
    bool IsSystem,
    int MemberCount,
    IReadOnlyList<string> PermissionCodes);

public sealed record TenantAppRoleDetail(
    string Id,
    string Code,
    string Name,
    string? Description,
    bool IsSystem,
    string CreatedAt,
    string UpdatedAt,
    int MemberCount,
    IReadOnlyList<string> PermissionCodes);

public sealed record TenantAppRoleGovernanceItem(
    string RoleId,
    string RoleCode,
    string RoleName,
    bool IsSystem,
    int MemberCount,
    int PermissionCount,
    bool HasPermissionCoverage);

public sealed record TenantAppRoleGovernanceOverview(
    string AppId,
    int TotalRoles,
    int SystemRoleCount,
    int CustomRoleCount,
    int TotalMembers,
    int CoveredMembers,
    int UncoveredMembers,
    decimal PermissionCoverageRate,
    IReadOnlyList<TenantAppRoleGovernanceItem> Roles);

public sealed record MigrationGovernanceOverview(
    string WindowStartedAt,
    long TotalApiHits,
    long LegacyRouteHits,
    long RewriteHits,
    long V1EntryHits,
    long V2EntryHits,
    long NotFoundCount,
    long FallbackCount,
    decimal NotFoundRate,
    decimal NewEntryCoverageRate);

public sealed record TenantAppRoleCreateRequest(
    string Code,
    string Name,
    string? Description,
    IReadOnlyList<string> PermissionCodes);

public sealed record TenantAppRoleUpdateRequest(
    string Name,
    string? Description);

public sealed record TenantAppRoleAssignPermissionsRequest(
    IReadOnlyList<string> PermissionCodes);

public sealed record RuntimeContextListItem(
    string Id,
    string AppKey,
    string PageKey,
    int SchemaVersion,
    string EnvironmentCode,
    bool IsActive);

public sealed record RuntimeContextDetail(
    string Id,
    string AppKey,
    string PageKey,
    int SchemaVersion,
    string EnvironmentCode,
    bool IsActive);

public sealed record RuntimeExecutionListItem(
    string Id,
    string WorkflowId,
    string? RuntimeContextId,
    string? ReleaseId,
    string? AppId,
    string Status,
    string StartedAt,
    string? CompletedAt,
    string? ErrorMessage,
    string? ErrorCategory);

public sealed record RuntimeExecutionStats(
    long Total,
    long Running,
    long Succeeded,
    long Failed,
    long Cancelled,
    double? AvgDurationMs,
    double? P95DurationMs,
    IReadOnlyDictionary<string, long> ErrorCategories);

public sealed record RuntimeExecutionDetail(
    string Id,
    string WorkflowId,
    string? RuntimeContextId,
    string? ReleaseId,
    string? AppId,
    string Status,
    string StartedAt,
    string? CompletedAt,
    string? InputsJson,
    string? OutputsJson,
    string? ErrorMessage);

public sealed record RuntimeExecutionDebugRequest(
    string NodeKey,
    string? InputsJson);

public sealed record RuntimeExecutionOperationResult(
    string Action,
    string ExecutionId,
    string Status,
    string Message,
    string? NewExecutionId);

public sealed record RuntimeExecutionTimeoutDiagnosis(
    string ExecutionId,
    string Status,
    string StartedAt,
    string? CompletedAt,
    double ElapsedSeconds,
    bool TimeoutRisk,
    string Diagnosis,
    IReadOnlyList<string> Suggestions);

public sealed record ResourceCenterGroupEntry(
    string ResourceId,
    string ResourceName,
    string ResourceType,
    string? Status,
    string? Description,
    string? NavigationPath = null,
    string? RelatedCatalogId = null,
    string? RelatedInstanceId = null,
    string? RelatedReleaseId = null,
    string? RelatedRuntimeContextId = null,
    string? RelatedExecutionId = null);

public sealed record ResourceCenterGroupItem(
    string GroupKey,
    string GroupName,
    int Total,
    IReadOnlyList<ResourceCenterGroupEntry> Items);

public sealed record ResourceCenterWarningItem(
    string AppInstanceId,
    string? AppName,
    string ErrorCode,
    string Message);

public sealed record ResourceCenterGroupsResponse(
    IReadOnlyList<ResourceCenterGroupItem> Groups,
    IReadOnlyList<ResourceCenterWarningItem> Warnings);

public sealed record ResourceCenterGroupSummaryItem(
    string GroupKey,
    string GroupName,
    int Total);

public sealed record ResourceCenterGroupsSummaryResponse(
    IReadOnlyList<ResourceCenterGroupSummaryItem> Groups,
    int WarningCount,
    string LastUpdatedAt);

public sealed record TenantAppConsumerItem(
    string TenantAppInstanceId,
    string AppKey,
    string Name,
    string Status);

public sealed record TenantDataSourceBindingRelationItem(
    string BindingId,
    string TenantAppInstanceId,
    string DataSourceId,
    string BindingType,
    bool IsActive,
    string? BoundAt,
    string? UpdatedAt,
    string Source);

public sealed record TenantDataSourceConsumptionItem(
    string DataSourceId,
    string Name,
    string DbType,
    bool IsActive,
    string Scope,
    string? ScopeAppId,
    string? ScopeAppName,
    int BoundTenantAppCount,
    IReadOnlyList<TenantAppConsumerItem> BoundTenantApps,
    IReadOnlyList<TenantDataSourceBindingRelationItem> BindingRelations,
    string? LastTestedAt,
    string? LastTestMessage,
    bool IsOrphan,
    bool IsDuplicate,
    bool IsInvalid,
    bool IsUnbound,
    string ImpactScope,
    string RepairSuggestion);

public sealed record ResourceCenterDataSourceConsumptionResponse(
    int PlatformDataSourceTotal,
    int AppScopedDataSourceTotal,
    int UnboundTenantAppTotal,
    IReadOnlyList<TenantDataSourceConsumptionItem> PlatformDataSources,
    IReadOnlyList<TenantDataSourceConsumptionItem> AppScopedDataSources,
    IReadOnlyList<TenantAppConsumerItem> UnboundTenantApps);

public sealed record TenantDataSourceConsumptionSummaryItem(
    string DataSourceId,
    string Name,
    string DbType,
    string Scope,
    string? ScopeAppId,
    string? ScopeAppName,
    int BoundTenantAppCount,
    IReadOnlyList<TenantAppConsumerItem> BoundTenantApps,
    string? LastTestedAt,
    bool IsOrphan,
    bool IsDuplicate,
    bool IsInvalid,
    bool IsUnbound);

public sealed record ResourceCenterDataSourceConsumptionSummaryResponse(
    int PlatformDataSourceTotal,
    int AppScopedDataSourceTotal,
    int UnboundTenantAppTotal,
    IReadOnlyList<TenantDataSourceConsumptionSummaryItem> PlatformDataSources,
    IReadOnlyList<TenantDataSourceConsumptionSummaryItem> AppScopedDataSources,
    IReadOnlyList<TenantAppConsumerItem> UnboundTenantApps,
    string LastUpdatedAt);

public sealed record DisableInvalidBindingRequest(
    string BindingId);

public sealed record SwitchPrimaryBindingRequest(
    string TenantAppInstanceId,
    string TargetDataSourceId,
    string? Note);

public sealed record UnbindOrphanBindingRequest(
    string BindingId);

public sealed record ResourceCenterRepairResult(
    string Action,
    string ResourceId,
    bool Success,
    string Message);

public sealed record ReleaseCenterListItem(
    string ReleaseId,
    string ApplicationCatalogId,
    string ApplicationCatalogName,
    string AppKey,
    int Version,
    string Status,
    string ReleasedAt,
    string? ReleaseNote);

public sealed record ReleaseCenterDetail(
    string ReleaseId,
    string ApplicationCatalogId,
    string ApplicationCatalogName,
    string AppKey,
    int Version,
    string Status,
    string ReleasedAt,
    string? ReleaseNote,
    string SnapshotJson);

public sealed record ReleaseDiffSummary(
    string ReleaseId,
    string? BaselineReleaseId,
    int AddedCount,
    int RemovedCount,
    int ChangedCount,
    IReadOnlyList<string> AddedKeys,
    IReadOnlyList<string> RemovedKeys,
    IReadOnlyList<string> ChangedKeys);

public sealed record ReleaseImpactSummary(
    string ReleaseId,
    string AppKey,
    int RuntimeRouteCount,
    int ActiveRuntimeRouteCount,
    int RuntimeContextCount,
    int RecentExecutionCount,
    int RunningExecutionCount,
    int FailedExecutionCount);

public sealed record ReleaseRollbackResult(
    string ManifestId,
    string TargetReleaseId,
    int TargetVersion,
    string? PreviousReleaseId,
    int? PreviousVersion,
    bool Switched,
    int ReboundRouteCount,
    string Result,
    string? Message);

public sealed record ReleasePreCheckResult(
    bool CanRelease,
    int PageCount,
    int TableCount,
    int RouteCount,
    string? BlockReason)
{
    public static ReleasePreCheckResult Pass(int pageCount, int tableCount, int routeCount)
        => new(true, pageCount, tableCount, routeCount, null);

    public static ReleasePreCheckResult Fail(string reason, int pageCount = 0, int tableCount = 0, int routeCount = 0)
        => new(false, pageCount, tableCount, routeCount, reason);
}

public sealed record AppPackageBuildResult(
    string ArtifactId,
    string ArtifactSha256,
    string PackagePath,
    string ManifestPath,
    string BuiltAt);

public sealed record ReleaseInstallResult(
    string ReleaseId,
    string TenantAppInstanceId,
    string InstallStatus,
    string RuntimeStatus,
    string HealthStatus,
    int? AssignedPort,
    int? CurrentPid,
    string? IngressUrl,
    string? LoginUrl,
    string? ArtifactId,
    string? ArtifactSha256,
    string InstalledAt,
    string? Message);

public sealed record ReleaseInstallStatusInfo(
    string ReleaseId,
    string TenantAppInstanceId,
    string InstallStatus,
    string RuntimeStatus,
    string HealthStatus,
    int? AssignedPort,
    int? CurrentPid,
    string? IngressUrl,
    string? LoginUrl,
    string? ArtifactId,
    string? ArtifactSha256,
    string LastUpdatedAt,
    string? Message);

public sealed record AppEntryInfo(
    string AppKey,
    string AppName,
    string? LogoUrl,
    string Theme,
    string LoginTitle,
    string AuthMode,
    string CallbackUrl,
    string RuntimeUrl,
    string LoginUrl);

public sealed record AppEntryLoginBeginRequest(
    string? RedirectUri);

public sealed record AppEntryLoginBeginResult(
    string AppKey,
    string LoginUrl,
    string RuntimeUrl,
    string CallbackUrl,
    string? RedirectUri);

public sealed record AppEntryLoginOptions(
    string AppKey,
    string AuthMode,
    string LoginTitle,
    string? LogoUrl,
    string Theme);

public sealed record RuntimeExecutionAuditTrailItem(
    string AuditId,
    string Actor,
    string Action,
    string Result,
    string Target,
    string OccurredAt);

public sealed record RuntimeRouteQuery(
    string? AppKey,
    string? PageKey,
    bool? IsActive,
    int PageIndex = 1,
    int PageSize = 20);

public sealed record CozeLayerMappingItem(
    string LayerKey,
    string LayerName,
    int Total,
    string Description);

public sealed record CozeLayerMappingOverview(
    IReadOnlyList<CozeLayerMappingItem> Layers);

public sealed record DebugLayerResourceItem(
    string ResourceKey,
    string ResourceName,
    string RequiredPermission,
    string Description);

public sealed record DebugLayerEmbedMetadata(
    string TenantId,
    string AppId,
    string? ProjectId,
    bool ProjectScopeEnabled,
    IReadOnlyList<DebugLayerResourceItem> Resources);

// ===== 应用级组织管理 Models =====

public sealed record AppDepartmentListItem(
    string Id,
    string Name,
    string Code,
    string? ParentId,
    int SortOrder);

public sealed record AppDepartmentDetail(
    string Id,
    string AppId,
    string Name,
    string Code,
    string? ParentId,
    int SortOrder);

public sealed record AppDepartmentCreateRequest(
    string Name,
    string Code,
    long? ParentId,
    int SortOrder);

public sealed record AppDepartmentUpdateRequest(
    string Name,
    string Code,
    long? ParentId,
    int SortOrder);

public sealed record AppPositionListItem(
    string Id,
    string Name,
    string Code,
    string? Description,
    bool IsActive,
    int SortOrder);

public sealed record AppPositionDetail(
    string Id,
    string AppId,
    string Name,
    string Code,
    string? Description,
    bool IsActive,
    int SortOrder);

public sealed record AppPositionCreateRequest(
    string Name,
    string Code,
    string? Description,
    bool IsActive,
    int SortOrder);

public sealed record AppPositionUpdateRequest(
    string Name,
    string? Description,
    bool IsActive,
    int SortOrder);

public sealed record AppProjectListItem(
    string Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive);

public sealed record AppProjectDetail(
    string Id,
    string AppId,
    string Code,
    string Name,
    string? Description,
    bool IsActive);

public sealed record AppProjectCreateRequest(
    string Code,
    string Name,
    string? Description,
    bool IsActive = true);

public sealed record AppProjectUpdateRequest(
    string Name,
    string? Description,
    bool IsActive);

// ===== 应用角色分配 Models =====

public sealed record AppRoleAssignmentDetail(
    string RoleId,
    string RoleCode,
    string RoleName,
    int DataScope,
    IReadOnlyList<string> DeptIds);

public sealed record AppRoleDataScopeRequest(
    int DataScope,
    IReadOnlyList<long>? DeptIds);

// ===== 应用角色页面分配 =====

public sealed record AppRolePagesRequest(
    IReadOnlyList<long> PageIds);

// ===== 应用角色字段权限 =====

public sealed record AppRoleFieldPermissionGroup(
    string TableKey,
    IReadOnlyList<AppRoleFieldPermissionItem> Fields);

public sealed record AppRoleFieldPermissionItem(
    string FieldName,
    bool CanView,
    bool CanEdit);

public sealed record AppRoleFieldPermissionsRequest(
    IReadOnlyList<AppRoleFieldPermissionGroup> Groups);

// ===== 应用级页面列表项 =====

public sealed record AppPageListItem(
    string Id,
    string PageKey,
    string Name,
    string? Description,
    string? RoutePath,
    long? ParentPageId,
    int SortOrder,
    bool IsPublished);

/// <summary>
/// 导航投影预留模型（P2）：当前阶段角色导航授权仍基于 AppPageListItem 与角色页面分配。
/// 如后续需要目录分组/外链/隐藏页，可在不改动角色授权主模型的前提下引入该投影层。
/// </summary>
public sealed record AppNavigationNode(
    string Id,
    string AppId,
    string? PageId,
    string? ParentId,
    string Title,
    string NodeType,
    string? RoutePath,
    string? ExternalUrl,
    bool IsHidden,
    int SortOrder);

// ===== 应用组织管理聚合 =====

public sealed record AppOrganizationWorkspaceResponse(
    string AppId,
    PagedResult<TenantAppMemberListItem> Members,
    TenantAppRoleGovernanceOverview RoleGovernance,
    IReadOnlyList<TenantAppRoleListItem> Roles,
    IReadOnlyList<AppDepartmentListItem> Departments,
    IReadOnlyList<AppPositionListItem> Positions,
    IReadOnlyList<AppProjectListItem> Projects);

public sealed record AppOrganizationAssignMembersRequest(
    IReadOnlyList<string> UserIds,
    IReadOnlyList<string> RoleIds,
    IReadOnlyList<string>? DepartmentIds,
    IReadOnlyList<string>? PositionIds,
    IReadOnlyList<string>? ProjectIds);

public sealed record AppOrganizationCreateMemberUserRequest(
    string Username,
    string Password,
    string DisplayName,
    string? Email,
    string? PhoneNumber,
    bool IsActive,
    IReadOnlyList<string> RoleIds,
    IReadOnlyList<string>? DepartmentIds,
    IReadOnlyList<string>? PositionIds,
    IReadOnlyList<string>? ProjectIds);

public sealed record AppOrganizationUpdateMemberRolesRequest(
    IReadOnlyList<string> RoleIds,
    IReadOnlyList<string>? DepartmentIds,
    IReadOnlyList<string>? PositionIds,
    IReadOnlyList<string>? ProjectIds);

public sealed record AppOrganizationResetMemberPasswordRequest(
    string NewPassword);

public sealed record AppOrganizationUpdateMemberProfileRequest(
    string DisplayName,
    string? Email,
    string? PhoneNumber,
    bool IsActive);

public sealed record AppOrganizationCreateRoleRequest(
    string Code,
    string Name,
    string? Description,
    IReadOnlyList<string>? PermissionCodes);

public sealed record AppOrganizationUpdateRoleRequest(
    string Name,
    string? Description);

public sealed record AppOrganizationCreateDepartmentRequest(
    string Name,
    string Code,
    string? ParentId,
    int SortOrder);

public sealed record AppOrganizationUpdateDepartmentRequest(
    string Name,
    string Code,
    string? ParentId,
    int SortOrder);

public sealed record AppOrganizationCreatePositionRequest(
    string Name,
    string Code,
    string? Description,
    bool IsActive,
    int SortOrder);

public sealed record AppOrganizationUpdatePositionRequest(
    string Name,
    string? Description,
    bool IsActive,
    int SortOrder);

public sealed record AppOrganizationCreateProjectRequest(
    string Code,
    string Name,
    string? Description,
    bool IsActive);

public sealed record AppOrganizationUpdateProjectRequest(
    string Name,
    string? Description,
    bool IsActive);

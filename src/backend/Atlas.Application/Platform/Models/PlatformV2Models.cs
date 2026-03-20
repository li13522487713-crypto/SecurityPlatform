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
    string? PublishedAt);

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
    string? DataSourceId);

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
    string? PublishedAt);

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
    string? DataSourceId);

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

public sealed record TenantAppMemberListItem(
    string UserId,
    string Username,
    string DisplayName,
    bool IsActive,
    string JoinedAt,
    IReadOnlyList<string> RoleIds,
    IReadOnlyList<string> RoleNames);

public sealed record TenantAppMemberDetail(
    string UserId,
    string Username,
    string DisplayName,
    string? Email,
    string? PhoneNumber,
    bool IsActive,
    string JoinedAt,
    IReadOnlyList<string> RoleIds,
    IReadOnlyList<string> RoleNames);

public sealed record TenantAppMemberAssignRequest(
    IReadOnlyList<long> UserIds,
    IReadOnlyList<long> RoleIds);

public sealed record TenantAppMemberUpdateRolesRequest(
    IReadOnlyList<long> RoleIds);

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
    string? ErrorMessage);

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

public sealed record RuntimeExecutionAuditTrailItem(
    string AuditId,
    string Actor,
    string Action,
    string Result,
    string Target,
    string OccurredAt);

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

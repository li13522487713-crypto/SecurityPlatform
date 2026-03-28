export interface TenantAppInstanceListItem {
  id: string;
  appKey: string;
  name: string;
  status: string;
  version: number;
  description?: string;
  category?: string;
  icon?: string;
  publishedAt?: string;
}

export interface TenantAppInstanceDetail extends TenantAppInstanceListItem {
  dataSourceId?: string;
}

export interface ApplicationCatalogListItem {
  id: string;
  catalogKey: string;
  name: string;
  status: string;
  version: number;
  description?: string;
  category?: string;
  icon?: string;
  publishedAt?: string;
}

export interface ApplicationCatalogDetail extends ApplicationCatalogListItem {
  dataSourceId?: string;
}

export interface TenantApplicationListItem {
  id: string;
  applicationCatalogId: string;
  applicationCatalogName: string;
  tenantAppInstanceId: string;
  appKey: string;
  name: string;
  status: string;
  openedAt: string;
  dataSourceId?: string;
}

export interface TenantApplicationDetail extends TenantApplicationListItem {
  updatedAt: string;
}

export interface TenantAppDataSourceBinding {
  tenantAppInstanceId: string;
  dataSourceId?: string;
  dataSourceName?: string;
  dbType?: string;
  dataSourceActive?: boolean;
  lastTestedAt?: string;
  bindingId?: string;
  bindingType?: string;
  bindingActive?: boolean;
  boundAt?: string;
  source?: string;
}

export interface TenantAppFileStorageSettings {
  tenantAppInstanceId: string;
  appId: string;
  effectiveBasePath: string;
  effectiveMinioBucketName: string;
  overrideBasePath?: string;
  overrideMinioBucketName?: string;
  inheritBasePath: boolean;
  inheritMinioBucketName: boolean;
}

export interface TenantAppFileStorageSettingsUpdateRequest {
  overrideBasePath?: string;
  overrideMinioBucketName?: string;
  inheritBasePath: boolean;
  inheritMinioBucketName: boolean;
}

export interface TenantAppMemberListItem {
  userId: string;
  username: string;
  displayName: string;
  isActive: boolean;
  joinedAt: string;
  roleIds: string[];
  roleNames: string[];
}

export interface TenantAppMemberDetail extends TenantAppMemberListItem {
  email?: string;
  phoneNumber?: string;
}

export interface TenantAppMemberAssignRequest {
  userIds: number[];
  roleIds: number[];
}

export interface TenantAppMemberUpdateRolesRequest {
  roleIds: number[];
}

export interface TenantAppRoleListItem {
  id: string;
  code: string;
  name: string;
  description?: string;
  isSystem: boolean;
  memberCount: number;
  permissionCodes: string[];
}

export interface TenantAppRoleDetail extends TenantAppRoleListItem {
  createdAt: string;
  updatedAt: string;
}

export interface TenantAppRoleGovernanceItem {
  roleId: string;
  roleCode: string;
  roleName: string;
  isSystem: boolean;
  memberCount: number;
  permissionCount: number;
  hasPermissionCoverage: boolean;
}

export interface TenantAppRoleGovernanceOverview {
  appId: string;
  totalRoles: number;
  systemRoleCount: number;
  customRoleCount: number;
  totalMembers: number;
  coveredMembers: number;
  uncoveredMembers: number;
  permissionCoverageRate: number;
  roles: TenantAppRoleGovernanceItem[];
}

export interface MigrationGovernanceOverview {
  windowStartedAt: string;
  totalApiHits: number;
  legacyRouteHits: number;
  rewriteHits: number;
  v1EntryHits: number;
  v2EntryHits: number;
  notFoundCount: number;
  fallbackCount: number;
  notFoundRate: number;
  newEntryCoverageRate: number;
}

export interface TenantAppRoleCreateRequest {
  code: string;
  name: string;
  description?: string;
  permissionCodes: string[];
}

export interface TenantAppRoleUpdateRequest {
  name: string;
  description?: string;
}

export interface TenantAppRoleAssignPermissionsRequest {
  permissionCodes: string[];
}

export interface RuntimeContextListItem {
  id: string;
  appKey: string;
  pageKey: string;
  schemaVersion: number;
  environmentCode: string;
  isActive: boolean;
}

export interface RuntimeContextDetail extends RuntimeContextListItem {}

export interface ResourceCenterGroupEntry {
  resourceId: string;
  resourceName: string;
  resourceType: string;
  status?: string;
  description?: string;
  navigationPath?: string;
  relatedCatalogId?: string;
  relatedInstanceId?: string;
  relatedReleaseId?: string;
  relatedRuntimeContextId?: string;
  relatedExecutionId?: string;
}

export interface ResourceCenterGroupItem {
  groupKey: string;
  groupName: string;
  total: number;
  items: ResourceCenterGroupEntry[];
}

export interface TenantAppConsumerItem {
  tenantAppInstanceId: string;
  appKey: string;
  name: string;
  status: string;
}

export interface TenantDataSourceConsumptionItem {
  dataSourceId: string;
  name: string;
  dbType: string;
  isActive: boolean;
  scope: "Platform" | "AppScoped";
  scopeAppId?: string;
  scopeAppName?: string;
  boundTenantAppCount: number;
  boundTenantApps: TenantAppConsumerItem[];
  bindingRelations: TenantDataSourceBindingRelationItem[];
  lastTestedAt?: string;
  lastTestMessage?: string;
  isOrphan: boolean;
  isDuplicate: boolean;
  isInvalid: boolean;
  isUnbound: boolean;
  impactScope: string;
  repairSuggestion: string;
}

export interface TenantDataSourceBindingRelationItem {
  bindingId: string;
  tenantAppInstanceId: string;
  dataSourceId: string;
  bindingType: string;
  isActive: boolean;
  boundAt?: string;
  updatedAt?: string;
  source: string;
}

export interface ResourceCenterDataSourceConsumptionResponse {
  platformDataSourceTotal: number;
  appScopedDataSourceTotal: number;
  unboundTenantAppTotal: number;
  platformDataSources: TenantDataSourceConsumptionItem[];
  appScopedDataSources: TenantDataSourceConsumptionItem[];
  unboundTenantApps: TenantAppConsumerItem[];
}

export interface ResourceCenterRepairResult {
  action: string;
  resourceId: string;
  success: boolean;
  message: string;
}

export interface ReleaseCenterListItem {
  releaseId: string;
  applicationCatalogId: string;
  applicationCatalogName: string;
  appKey: string;
  version: number;
  status: string;
  releasedAt: string;
  releaseNote?: string;
}

export interface ReleaseCenterDetail extends ReleaseCenterListItem {
  snapshotJson: string;
}

export interface ReleaseDiffSummary {
  releaseId: string;
  baselineReleaseId?: string;
  addedCount: number;
  removedCount: number;
  changedCount: number;
  addedKeys: string[];
  removedKeys: string[];
  changedKeys: string[];
}

export interface ReleaseImpactSummary {
  releaseId: string;
  appKey: string;
  runtimeRouteCount: number;
  activeRuntimeRouteCount: number;
  runtimeContextCount: number;
  recentExecutionCount: number;
  runningExecutionCount: number;
  failedExecutionCount: number;
}

export interface ReleaseRollbackResult {
  manifestId: string;
  targetReleaseId: string;
  targetVersion: number;
  previousReleaseId?: string;
  previousVersion?: number;
  switched: boolean;
  reboundRouteCount: number;
  result: string;
  message?: string;
}

export interface RuntimeExecutionAuditTrailItem {
  auditId: string;
  actor: string;
  action: string;
  result: string;
  target: string;
  occurredAt: string;
}

export interface RuntimeExecutionListItem {
  id: string;
  workflowId: string;
  runtimeContextId?: string;
  releaseId?: string;
  appId?: string;
  status: string;
  startedAt: string;
  completedAt?: string;
  errorMessage?: string;
}

export interface RuntimeExecutionDetail extends RuntimeExecutionListItem {
  inputsJson?: string;
  outputsJson?: string;
}

export interface RuntimeExecutionDebugRequest {
  nodeKey: string;
  inputsJson?: string;
}

export interface RuntimeExecutionOperationResult {
  action: string;
  executionId: string;
  status: string;
  message: string;
  newExecutionId?: string;
}

export interface RuntimeExecutionTimeoutDiagnosis {
  executionId: string;
  status: string;
  startedAt: string;
  completedAt?: string;
  elapsedSeconds: number;
  timeoutRisk: boolean;
  diagnosis: string;
  suggestions: string[];
}

export interface CozeLayerMappingItem {
  layerKey: string;
  layerName: string;
  total: number;
  description: string;
}

export interface CozeLayerMappingOverview {
  layers: CozeLayerMappingItem[];
}

export interface DebugLayerResourceItem {
  resourceKey: string;
  resourceName: string;
  requiredPermission: string;
  description: string;
}

export interface DebugLayerEmbedMetadata {
  tenantId: string;
  appId: string;
  projectId?: string;
  projectScopeEnabled: boolean;
  resources: DebugLayerResourceItem[];
}

// ===== 应用级组织管理类型 =====

export interface AppDepartmentListItem {
  id: string;
  name: string;
  code: string;
  parentId?: string;
  sortOrder: number;
}

export interface AppDepartmentDetail extends AppDepartmentListItem {
  appId: string;
}

export interface AppDepartmentCreateRequest {
  name: string;
  code: string;
  parentId?: number;
  sortOrder: number;
}

export interface AppDepartmentUpdateRequest {
  name: string;
  code: string;
  parentId?: number;
  sortOrder: number;
}

export interface AppPositionListItem {
  id: string;
  name: string;
  code: string;
  description?: string;
  isActive: boolean;
  sortOrder: number;
}

export interface AppPositionDetail extends AppPositionListItem {
  appId: string;
}

export interface AppPositionCreateRequest {
  name: string;
  code: string;
  description?: string;
  isActive: boolean;
  sortOrder: number;
}

export interface AppPositionUpdateRequest {
  name: string;
  description?: string;
  isActive: boolean;
  sortOrder: number;
}

export interface AppProjectListItem {
  id: string;
  code: string;
  name: string;
  description?: string;
  isActive: boolean;
}

export interface AppProjectDetail extends AppProjectListItem {
  appId: string;
}

export interface AppProjectCreateRequest {
  code: string;
  name: string;
  description?: string;
  isActive: boolean;
}

export interface AppProjectUpdateRequest {
  name: string;
  description?: string;
  isActive: boolean;
}

// ===== 应用角色分配类型 =====

export interface AppRoleAssignmentDetail {
  roleId: string;
  roleCode: string;
  roleName: string;
  dataScope: number;
  deptIds: string[];
}

export interface AppRoleDataScopeRequest {
  dataScope: number;
  deptIds?: number[];
}

// ===== 应用角色页面分配 =====

export interface AppRolePagesRequest {
  pageIds: number[];
}

// ===== 应用角色字段权限 =====

export interface AppRoleFieldPermissionItem {
  fieldName: string;
  canView: boolean;
  canEdit: boolean;
}

export interface AppRoleFieldPermissionGroup {
  tableKey: string;
  fields: AppRoleFieldPermissionItem[];
}

export interface AppRoleFieldPermissionsRequest {
  groups: AppRoleFieldPermissionGroup[];
}

// ===== 应用级页面 =====

export interface AppPageListItem {
  id: string;
  pageKey: string;
  name: string;
  description?: string;
  routePath?: string;
  parentPageId?: number;
  sortOrder: number;
  isPublished: boolean;
}

// ===== 组织管理聚合 =====

export interface AppOrganizationWorkspaceResponse {
  appId: string;
  members: import("@/types/api").PagedResult<TenantAppMemberListItem>;
  roleGovernance: TenantAppRoleGovernanceOverview;
  roles: TenantAppRoleListItem[];
  departments: AppDepartmentListItem[];
  positions: AppPositionListItem[];
  projects: AppProjectListItem[];
}

export interface AppOrganizationAssignMembersRequest {
  userIds: string[];
  roleIds: string[];
}

export interface AppOrganizationUpdateMemberRolesRequest {
  roleIds: string[];
}

export interface AppOrganizationCreateRoleRequest {
  code: string;
  name: string;
  description?: string;
  permissionCodes?: string[];
}

export interface AppOrganizationUpdateRoleRequest {
  name: string;
  description?: string;
}

export interface AppOrganizationCreateDepartmentRequest {
  name: string;
  code: string;
  parentId?: string;
  sortOrder: number;
}

export interface AppOrganizationUpdateDepartmentRequest {
  name: string;
  code: string;
  parentId?: string;
  sortOrder: number;
}

export interface AppOrganizationCreatePositionRequest {
  name: string;
  code: string;
  description?: string;
  isActive: boolean;
  sortOrder: number;
}

export interface AppOrganizationUpdatePositionRequest {
  name: string;
  description?: string;
  isActive: boolean;
  sortOrder: number;
}

export interface AppOrganizationCreateProjectRequest {
  code: string;
  name: string;
  description?: string;
  isActive: boolean;
}

export interface AppOrganizationUpdateProjectRequest {
  name: string;
  description?: string;
  isActive: boolean;
}

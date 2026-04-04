/**
 * 控制台 v2 API 契约类型（与 Atlas.WebApp platform-v2 / lowcode 对齐）。
 */

// ─── 租户应用实例 ───

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
  pageCount: number;
}

// ─── 应用目录 / 租户开通 ───

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
  isBound: boolean;
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

// ─── 资源中心 ───

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

export interface ResourceCenterWarningItem {
  appInstanceId: string;
  appName?: string;
  errorCode: string;
  message: string;
}

export interface ResourceCenterGroupsResponse {
  groups: ResourceCenterGroupItem[];
  warnings: ResourceCenterWarningItem[];
}

export interface ResourceCenterGroupSummaryItem {
  groupKey: string;
  groupName: string;
  total: number;
}

export interface ResourceCenterGroupsSummaryResponse {
  groups: ResourceCenterGroupSummaryItem[];
  warningCount: number;
  lastUpdatedAt: string;
}

export interface TenantAppConsumerItem {
  tenantAppInstanceId: string;
  appKey: string;
  name: string;
  status: string;
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

export interface ResourceCenterDataSourceConsumptionResponse {
  platformDataSourceTotal: number;
  appScopedDataSourceTotal: number;
  unboundTenantAppTotal: number;
  platformDataSources: TenantDataSourceConsumptionItem[];
  appScopedDataSources: TenantDataSourceConsumptionItem[];
  unboundTenantApps: TenantAppConsumerItem[];
}

export interface TenantDataSourceConsumptionSummaryItem {
  dataSourceId: string;
  name: string;
  dbType: string;
  scope: "Platform" | "AppScoped";
  scopeAppId?: string;
  scopeAppName?: string;
  boundTenantAppCount: number;
  boundTenantApps: TenantAppConsumerItem[];
  lastTestedAt?: string;
  isOrphan: boolean;
  isDuplicate: boolean;
  isInvalid: boolean;
  isUnbound: boolean;
}

export interface ResourceCenterDataSourceConsumptionSummaryResponse {
  platformDataSourceTotal: number;
  appScopedDataSourceTotal: number;
  unboundTenantAppTotal: number;
  platformDataSources: TenantDataSourceConsumptionSummaryItem[];
  appScopedDataSources: TenantDataSourceConsumptionSummaryItem[];
  unboundTenantApps: TenantAppConsumerItem[];
  lastUpdatedAt: string;
}

export interface ResourceCenterRepairResult {
  action: string;
  resourceId: string;
  success: boolean;
  message: string;
}

// ─── 低代码应用（租户应用实例 API 使用）───

export interface LowCodeAppCreateRequest {
  appKey: string;
  name: string;
  description?: string;
  category?: string;
  icon?: string;
  dataSourceId?: number;
}

export interface LowCodeAppUpdateRequest {
  name: string;
  description?: string;
  category?: string;
  icon?: string;
  dataSourceId?: number | null;
  unbindDataSource?: boolean;
}

export interface LowCodeAppEntityAliasItem {
  entityType: string;
  singularAlias: string;
  pluralAlias: string;
}

export interface LowCodeAppEntityAliasesUpdateRequest {
  items: LowCodeAppEntityAliasItem[];
}

export interface LowCodeAppDataSourceInfo {
  dataSourceId?: string;
  name?: string;
  dbType?: string;
  maxPoolSize?: number;
  connectionTimeoutSeconds?: number;
  lastTestSuccess?: boolean;
  lastTestedAt?: string;
  lastTestMessage?: string;
}

export interface LowCodeAppExportPagePackage {
  id: string;
  pageKey: string;
  name: string;
  pageType: string;
  schemaJson: string;
  routePath?: string;
  description?: string;
  icon?: string;
  sortOrder: number;
  parentPageId?: string;
  permissionCode?: string;
  dataTableKey?: string;
  isPublished: boolean;
}

export interface LowCodeAppExportPageVersionPackage {
  id: string;
  pageId: string;
  snapshotVersion: number;
  pageKey: string;
  name: string;
  pageType: string;
  schemaJson: string;
  routePath?: string;
  description?: string;
  icon?: string;
  sortOrder: number;
  parentPageId?: string;
  permissionCode?: string;
  dataTableKey?: string;
  createdAt: string;
  createdBy: number;
}

export interface LowCodeAppExportPackage {
  appKey: string;
  name: string;
  description?: string;
  category?: string;
  icon?: string;
  status: string;
  configJson?: string;
  pages: LowCodeAppExportPagePackage[];
  pageVersions: LowCodeAppExportPageVersionPackage[];
}

export interface LowCodeAppImportRequest {
  package: LowCodeAppExportPackage;
  conflictStrategy: "Rename" | "Overwrite" | "Skip";
  keySuffix?: string;
}

export interface LowCodeAppImportResult {
  appId: string;
  appKey: string;
  skipped: boolean;
  overwritten: boolean;
  importedPageCount: number;
  importedVersionCount: number;
}

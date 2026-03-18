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

export interface ResourceCenterGroupEntry {
  resourceId: string;
  resourceName: string;
  resourceType: string;
  status?: string;
  description?: string;
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

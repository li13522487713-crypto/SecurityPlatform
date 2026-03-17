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

export interface TenantAppDataSourceBinding {
  tenantAppInstanceId: string;
  dataSourceId?: string;
  dataSourceName?: string;
  dbType?: string;
  dataSourceActive?: boolean;
  lastTestedAt?: string;
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

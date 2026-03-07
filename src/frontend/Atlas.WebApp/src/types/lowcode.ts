// ─── 表单定义 ───

export interface FormDefinitionListItem {
  id: string;
  name: string;
  description?: string;
  category?: string;
  version: number;
  status: string;
  createdAt: string;
  updatedAt: string;
  createdBy: number;
  dataTableKey?: string;
  icon?: string;
  publishedAt?: string;
}

export interface FormDefinitionDetail {
  id: string;
  name: string;
  description?: string;
  category?: string;
  schemaJson: string;
  version: number;
  status: string;
  createdAt: string;
  updatedAt: string;
  createdBy: number;
  updatedBy: number;
  dataTableKey?: string;
  icon?: string;
  publishedAt?: string;
  publishedBy?: number;
}

export interface FormDefinitionCreateRequest {
  name: string;
  description?: string;
  category?: string;
  schemaJson: string;
  dataTableKey?: string;
  icon?: string;
}

export interface FormDefinitionUpdateRequest {
  name: string;
  description?: string;
  category?: string;
  schemaJson: string;
  dataTableKey?: string;
  icon?: string;
}

export interface FormDefinitionVersionListItem {
  id: string;
  formDefinitionId: string;
  snapshotVersion: number;
  name: string;
  description?: string;
  category?: string;
  dataTableKey?: string;
  icon?: string;
  createdBy: number;
  createdAt: string;
}

export interface FormDefinitionVersionDetail extends FormDefinitionVersionListItem {
  schemaJson: string;
}

// ─── 低代码应用 ───

export interface LowCodeAppListItem {
  id: string;
  appKey: string;
  name: string;
  description?: string;
  category?: string;
  icon?: string;
  version: number;
  status: string;
  createdAt: string;
  createdBy: number;
  publishedAt?: string;
}

export interface LowCodeAppDetail {
  id: string;
  appKey: string;
  name: string;
  description?: string;
  category?: string;
  icon?: string;
  version: number;
  status: string;
  configJson?: string;
  createdAt: string;
  updatedAt: string;
  createdBy: number;
  updatedBy: number;
  publishedAt?: string;
  publishedBy?: number;
  pages: LowCodePageListItem[];
}

export interface LowCodeAppCreateRequest {
  appKey: string;
  name: string;
  description?: string;
  category?: string;
  icon?: string;
}

export interface LowCodeAppUpdateRequest {
  name: string;
  description?: string;
  category?: string;
  icon?: string;
}

export interface LowCodeAppVersionListItem {
  id: string;
  appId: string;
  version: number;
  actionType: string;
  sourceVersionId?: string;
  note?: string;
  createdAt: string;
  createdBy: number;
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

// ─── 低代码页面 ───

export interface LowCodePageListItem {
  id: string;
  appId: string;
  pageKey: string;
  name: string;
  pageType: string;
  routePath?: string;
  description?: string;
  icon?: string;
  sortOrder: number;
  parentPageId?: string;
  version: number;
  isPublished: boolean;
  createdAt: string;
  permissionCode?: string;
  dataTableKey?: string;
}

export interface LowCodePageDetail {
  id: string;
  appId: string;
  pageKey: string;
  name: string;
  pageType: string;
  schemaJson: string;
  routePath?: string;
  description?: string;
  icon?: string;
  sortOrder: number;
  parentPageId?: string;
  version: number;
  isPublished: boolean;
  publishedVersion?: number;
  createdAt: string;
  updatedAt: string;
  createdBy: number;
  updatedBy: number;
  permissionCode?: string;
  dataTableKey?: string;
}

export interface LowCodePageVersionListItem {
  id: string;
  pageId: string;
  snapshotVersion: number;
  createdAt: string;
  createdBy: number;
}

export interface LowCodePageRuntimeSchema {
  pageId: string;
  pageKey: string;
  name: string;
  schemaJson: string;
  version: number;
  mode: string;
}

export interface LowCodeEnvironmentListItem {
  id: string;
  appId: string;
  name: string;
  code: string;
  description?: string;
  isDefault: boolean;
  isActive: boolean;
  updatedAt: string;
}

export interface LowCodeEnvironmentDetail extends LowCodeEnvironmentListItem {
  variablesJson: string;
  createdAt: string;
  createdBy: number;
  updatedBy: number;
}

export interface LowCodeEnvironmentCreateRequest {
  name: string;
  code: string;
  description?: string;
  isDefault: boolean;
  variablesJson: string;
}

export interface LowCodeEnvironmentUpdateRequest {
  name: string;
  description?: string;
  isDefault: boolean;
  isActive: boolean;
  variablesJson: string;
}

export interface LowCodePageTreeNode extends LowCodePageListItem {
  children: LowCodePageTreeNode[];
}

export interface LowCodePageCreateRequest {
  pageKey: string;
  name: string;
  pageType: string;
  schemaJson: string;
  routePath?: string;
  description?: string;
  icon?: string;
  sortOrder: number;
  parentPageId?: number;
  permissionCode?: string;
  dataTableKey?: string;
}

export interface LowCodePageUpdateRequest {
  name: string;
  pageType: string;
  schemaJson: string;
  routePath?: string;
  description?: string;
  icon?: string;
  sortOrder: number;
  parentPageId?: number;
  permissionCode?: string;
  dataTableKey?: string;
}

// ─── 消息模板 ───

export interface MessageTemplateListItem {
  id: string;
  name: string;
  channel: string;
  eventType: string;
  status: string;
  createdAt: string;
}

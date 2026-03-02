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
  createdAt: string;
  updatedAt: string;
  createdBy: number;
  updatedBy: number;
  permissionCode?: string;
  dataTableKey?: string;
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

/** 应用运行端低代码运行时所需的最小类型（与后端契约对齐）。 */

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

export interface LowCodeAppDetail {
  id: string;
  appKey: string;
  name: string;
  description?: string;
  category?: string;
  icon?: string;
  dataSourceId?: string;
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

export interface LowCodePageRuntimeSchema {
  pageId: string;
  pageKey: string;
  name: string;
  schemaJson: string;
  version: number;
  mode: string;
}

import type { LowCodePageListItem } from "@/types/platform-console";

export interface LowCodePageTreeNode extends LowCodePageListItem {
  children?: LowCodePageTreeNode[];
}

export interface LowCodePageDetail extends LowCodePageListItem {
  schemaJson?: string;
}

export interface LowCodePageRuntimeSchema {
  pageId: string;
  schema: Record<string, object | string | number | boolean | null>;
  schemaJson?: string;
  version: number;
}

export interface LowCodePageCreateRequest {
  pageKey: string;
  name: string;
  pageType: string;
  routePath?: string;
  description?: string;
  sortOrder?: number;
}

export interface LowCodePageUpdateRequest {
  name: string;
  pageType: string;
  routePath?: string;
  description?: string;
  sortOrder?: number;
}

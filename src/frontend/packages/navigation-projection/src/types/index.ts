export type NavigationHostMode = "platform" | "app" | "runtime";

export interface NavigationProjectionScope {
  tenantId: string;
  appInstanceId?: string;
  appKey?: string;
}

export interface NavigationProjectionItem {
  key: string;
  title: string;
  path: string;
  permissionCode?: string;
  order: number;
  sourceRefs: string[];
}

export interface NavigationProjectionGroup {
  groupKey: string;
  groupTitle: string;
  items: NavigationProjectionItem[];
}

export interface NavigationProjectionResponse {
  hostMode: NavigationHostMode;
  scope: NavigationProjectionScope;
  groups: NavigationProjectionGroup[];
  generatedAt: string;
}

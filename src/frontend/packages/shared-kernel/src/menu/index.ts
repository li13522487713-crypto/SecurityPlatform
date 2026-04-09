export interface MenuMeta {
  icon?: string;
  hidden?: boolean;
  order?: number;
}

export interface MenuNode {
  key: string;
  title: string;
  path: string;
  permissionCode?: string;
  children?: MenuNode[];
  meta?: MenuMeta;
}

export interface NavigationProjection {
  scope: "platform" | "app" | "runtime";
  items: MenuNode[];
  generatedAt: string;
}

/// <reference types="vite/client" />

declare module "*.vue" {
  import type { DefineComponent } from "vue";
  const component: DefineComponent<{}, {}, any>;
  export default component;
}

// 扩展 Vue Router RouteMeta 类型
import "vue-router";
declare module "vue-router" {
  interface RouteMeta {
    requiresAuth?: boolean;
    requiresPermission?: string;
    fullscreen?: boolean;
    amisKey?: string;
  }
}
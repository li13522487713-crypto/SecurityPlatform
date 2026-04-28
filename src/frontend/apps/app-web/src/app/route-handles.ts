import type { TRouteConfigGlobal } from "@coze-arch/bot-hooks";

export type AppRouteHandle = Partial<TRouteConfigGlobal>;

function createHandle(config: AppRouteHandle): AppRouteHandle {
  return config;
}

export const ROOT_ROUTE_HANDLE = createHandle({});
export const SIGN_ROUTE_HANDLE = createHandle({
  hasSider: false,
  requireAuth: false
});
export const STATUS_ROUTE_HANDLE = createHandle({
  hasSider: false,
  requireAuth: false
});
/**
 * 系统初始化与迁移控制台路由 handle。
 * 永久免登录，由控制台内部的二次认证（恢复密钥 / BootstrapAdmin）保护。
 */
export const SETUP_CONSOLE_ROUTE_HANDLE = createHandle({
  hasSider: false,
  requireAuth: false
});
export const WORKSPACE_LIST_ROUTE_HANDLE = createHandle({
  hasSider: false,
  requireAuth: true,
  menuKey: "workspace"
});
export const WORKSPACE_SHELL_ROUTE_HANDLE = createHandle({
  hasSider: true,
  requireAuth: true,
  responsive: true
});
export const WORKSPACE_DASHBOARD_ROUTE_HANDLE = createHandle({
  hasSider: true,
  requireAuth: true,
  menuKey: "dashboard",
  responsive: true
});
export const WORKSPACE_DEVELOP_ROUTE_HANDLE = createHandle({
  hasSider: true,
  requireAuth: true,
  menuKey: "develop",
  responsive: true
});
export const WORKSPACE_LIBRARY_ROUTE_HANDLE = createHandle({
  hasSider: true,
  requireAuth: true,
  menuKey: "library",
  responsive: true
});
export const WORKSPACE_MANAGE_ROUTE_HANDLE = createHandle({
  hasSider: true,
  requireAuth: true,
  menuKey: "manage",
  responsive: true
});
export const WORKSPACE_SETTINGS_ROUTE_HANDLE = createHandle({
  hasSider: true,
  requireAuth: true,
  menuKey: "settings",
  responsive: true
});
export const WORKSPACE_WORKFLOW_ROUTE_HANDLE = createHandle({
  hasSider: true,
  requireAuth: true,
  menuKey: "develop",
  subMenuKey: "workflow",
  responsive: true
});
export const MENDIX_STUDIO_ROUTE_HANDLE = createHandle({
  hasSider: true,
  requireAuth: true,
  menuKey: "develop",
  subMenuKey: "mendix-studio",
  responsive: true
});
export const WORKSPACE_CHATFLOW_ROUTE_HANDLE = createHandle({
  hasSider: true,
  requireAuth: true,
  menuKey: "develop",
  subMenuKey: "chatflow",
  responsive: true
});
export const STANDALONE_WORKFLOW_ROUTE_HANDLE = createHandle({
  hasSider: false,
  requireAuth: true,
  menuKey: "develop",
  subMenuKey: "workflow",
  responsive: true
});
export const EXPLORE_ROUTE_HANDLE = createHandle({
  hasSider: false,
  requireAuth: true,
  menuKey: "explore"
});

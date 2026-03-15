import type { RouteLocationMatched, RouteLocationNormalizedLoaded } from "vue-router";
import { i18n } from "@/i18n";

type NavigableMeta = {
  titleKey?: string;
  title?: unknown;
} | null | undefined;

const titleKeyByPath: Record<string, string> = {
  "/login": "route.login",
  "/register": "route.register",
  "/profile": "route.profile",
  "/console": "route.console",
  "/console/apps": "route.consoleApps",
  "/console/resources": "route.consoleResources",
  "/console/releases": "route.consoleReleases",
  "/console/tools": "route.consoleTools",
  "/console/datasources": "route.consoleDatasources",
  "/console/settings/system/configs": "route.consoleSystemConfigs",
  "/system/notifications": "route.notifications",
  "/system/notifications/manage": "route.notificationsManage",
  "/settings/system/dict-types": "route.dictTypes",
  "/settings/system/datasources": "route.datasources",
  "/settings/system/configs": "route.systemConfigs",
  "/settings/ai/model-configs": "route.modelConfigs",
  "/settings/auth/roles": "route.roles",
  "/settings/system/plugins": "route.plugins",
  "/settings/system/webhooks": "route.webhooks",
  "/monitor/message-queue": "route.messageQueue",
  "/monitor/server-info": "route.serverInfo",
  "/monitor/scheduled-jobs": "route.scheduledJobs",
  "/monitor/writeback-failures": "route.writebackMonitor",
  "/system/login-logs": "route.loginLogs",
  "/system/online-users": "route.onlineUsers",
  "/settings/license": "route.license",
  "/lowcode/apps": "route.lowcodeApps",
  "/lowcode/forms": "route.forms",
  "/lowcode/templates": "route.templateMarket",
  "/workflow": "route.workflowList",
  "/approval/designer": "route.approvalDesigner",
  "/approval/flows/manage": "route.approvalFlowManage",
  "/approval/flows": "route.approvalFlows",
  "/approval/workspace": "route.approvalWorkspace",
  "/settings/org/tenants": "route.tenants",
  "/settings/org/departments": "route.departments",
  "/settings/org/positions": "route.positions",
  "/settings/org/users": "route.users",
  "/settings/auth/menus": "route.menus",
  "/settings/projects": "route.projects",
  "/assets": "route.assets",
  "/audit": "route.audit",
  "/alert": "route.alert",
  "/ai/variables": "route.aiVariables",
  "/ai/open-platform": "route.aiOpenPlatform",
  "/ai/workspace": "route.aiWorkspace",
  "/ai/library": "route.aiLibrary",
  "/ai/devops/test-sets": "route.aiTestSets",
  "/ai/devops/mock-sets": "route.aiMockSets",
  "/ai/shortcuts": "route.aiShortcuts",
  "/ai/search": "route.aiSearch",
  "/ai/marketplace": "route.aiMarketplace"
};

const titleKeyByTitle: Record<string, string> = {
  "登录": "route.login",
  "注册": "route.register",
  "个人中心": "route.profile",
  "平台控制台": "route.console",
  "应用中心": "route.consoleApps",
  "资源中心": "route.consoleResources",
  "发布中心": "route.consoleReleases",
  "工具授权中心": "route.consoleTools",
  "数据源管理": "route.datasources",
  "系统设置": "route.consoleSystemConfigs",
  "应用工作台": "route.appWorkspace",
  "应用仪表盘": "route.appDashboard",
  "应用设计器": "route.appBuilder",
  "应用设置": "route.appSettings",
  "页面管理": "route.appPages",
  "表单管理": "route.forms",
  "流程管理": "route.processManage",
  "数据管理": "route.dataManage",
  "权限入口": "route.permissionsEntry",
  "应用运行态": "route.appRuntime",
  "运行交付面": "route.runtimeDelivery",
  "流程详情": "route.processDetail",
  "通知中心": "route.notifications",
  "公告管理": "route.notificationsManage",
  "字典管理": "route.dictTypes",
  "参数配置": "route.systemConfigs",
  "模型配置": "route.modelConfigs",
  "变量管理": "route.aiVariables",
  "开放平台": "route.aiOpenPlatform",
  "AI 工作台": "route.aiWorkspace",
  "资源库": "route.aiLibrary",
  "测试集": "route.aiTestSets",
  "Mock 集": "route.aiMockSets",
  "快捷命令": "route.aiShortcuts",
  "统一搜索": "route.aiSearch",
  "应用市场": "route.aiMarketplace",
  "Agent 编辑": "route.aiAgentEdit",
  "角色管理": "route.roles",
  "插件市场": "route.pluginMarket",
  "插件管理": "route.plugins",
  "Webhook 管理": "route.webhooks",
  "消息队列监控": "route.messageQueue",
  "服务器监控": "route.serverInfo",
  "定时任务": "route.scheduledJobs",
  "登录日志": "route.loginLogs",
  "在线用户": "route.onlineUsers",
  "授权管理": "route.license",
  "低代码应用": "route.lowcodeApps",
  "模板市场": "route.templateMarket",
  "表单设计器": "route.formDesigner",
  "回写监控": "route.writebackMonitor",
  "工作流管理": "route.workflowList",
  "工作流设计器": "route.workflowEditor",
  "流程设计器": "route.approvalDesigner",
  "流程发布总览": "route.approvalFlowManage",
  "流程定义列表": "route.approvalFlows",
  "所有审批实例": "route.approvalInstancesManage",
  "审批工作台": "route.approvalWorkspace",
  "租户管理": "route.tenants",
  "组织架构": "route.departments",
  "职位名称": "route.positions",
  "员工管理": "route.users",
  "菜单管理": "route.menus",
  "项目管理": "route.projects",
  "资产管理": "route.assets",
  "审计日志": "route.audit",
  "告警管理": "route.alert",
  "首页": "route.home"
};

function translateByKey(key: string, fallback?: string): string {
  const composer = i18n.global as unknown as { t: (messageKey: string) => string };
  const translated = composer.t(key);
  if (translated !== key) {
    return translated;
  }
  return fallback ?? key;
}

export function resolveTitleKey(
  meta?: NavigableMeta,
  path?: string | null,
  fallbackTitle?: string | null
): string | null {
  if (typeof meta?.titleKey === "string" && meta.titleKey.length > 0) {
    return meta.titleKey;
  }
  if (path && titleKeyByPath[path]) {
    return titleKeyByPath[path];
  }
  if (fallbackTitle && titleKeyByTitle[fallbackTitle]) {
    return titleKeyByTitle[fallbackTitle];
  }
  return null;
}

export function resolveRouteTitle(
  meta?: NavigableMeta,
  path?: string | null,
  fallbackTitle?: string | null
): string {
  const key = resolveTitleKey(meta, path, fallbackTitle);
  if (key) {
    return translateByKey(key, fallbackTitle ?? undefined);
  }
  return fallbackTitle ?? "";
}

export function resolveBreadcrumbTitle(record: RouteLocationMatched): string {
  return resolveRouteTitle(record.meta, record.path, String(record.meta?.title ?? ""));
}

export function applyDocumentTitle(route: RouteLocationNormalizedLoaded): void {
  const title = resolveRouteTitle(route.meta, route.path, typeof route.meta.title === "string" ? route.meta.title : "");
  document.title = title ? `${title} - Atlas Security Platform` : "Atlas Security Platform";
}

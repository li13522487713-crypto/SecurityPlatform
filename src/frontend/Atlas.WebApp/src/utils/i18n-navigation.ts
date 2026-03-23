import type { RouteLocationMatched, RouteLocationNormalizedLoaded } from "vue-router";
import { i18n, translate } from "@/i18n";

type NavigableMeta = {
  titleKey?: string;
  title?: unknown;
} | null | undefined;

const titleKeyByPath: Record<string, string> = {
  "/index": "route.workspace",
  "/login": "route.login",
  "/register": "route.register",
  "/profile": "route.profile",
  "/console": "route.console",
  "/console/apps": "route.consoleApps",
  "/console/catalog": "route.consoleCatalog",
  "/console/tenant-applications": "route.consoleTenantApplications",
  "/console/runtime-contexts": "route.consoleRuntimeContexts",
  "/console/runtime-executions": "route.consoleRuntimeExecutions",
  "/console/resources": "route.consoleResources",
  "/console/resources/datasource-consumption": "route.consoleDatasourceConsumption",
  "/console/releases": "route.consoleReleases",
  "/console/debug": "route.consoleDebugLayer",
  "/console/migration-governance": "route.consoleMigrationGovernance",
  "/console/tools": "route.consoleTools",
  "/console/datasources": "route.consoleDatasources",
  "/console/settings/system/configs": "route.consoleSystemConfigs",
  "/apps/:appId/run/:pageKey": "route.appRuntime",
  "/apps/:appId/evaluations/datasets": "route.aiTestSets",
  "/apps/:appId/evaluations/tasks": "route.aiEvaluationTasks",
  "/apps/:appId/evaluations/reports/:taskId": "route.aiEvaluationReport",
  "/r/:appKey/:pageKey": "route.runtimeDelivery",
  "/apps/:appId/agents/:id/edit": "route.aiAgentEdit",
  "/apps/:appId/workflows/:id/editor": "route.workflowEditor",
  "/apps/:appId/forms/:id/designer": "route.formDesigner",
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
  "/ai/devops/evaluations/tasks": "route.aiEvaluationTasks",
  "/ai/devops/evaluations/reports/:taskId": "route.aiEvaluationReport",
  "/ai/devops/mock-sets": "route.aiMockSets",
  "/ai/shortcuts": "route.aiShortcuts",
  "/ai/search": "route.aiSearch",
  "/ai/marketplace": "route.aiMarketplace",
  "/process/start": "route.approvalStart",
  "/process/inbox": "route.approvalInbox",
  "/process/done": "route.approvalDone",
  "/process/my-requests": "route.approvalMyRequests",
  "/process/cc": "route.approvalCc",
  "/process/manage/flows": "route.approvalFlowManage",
  "/process/manage/instances": "route.approvalInstancesManage",
  "/workflow/designer": "route.workflowDesigner",
  "/settings/auth/pats": "route.personalAccessTokens",
  "/ai/workflows": "route.aiWorkflowListGlobal",
  "/ai/knowledge-bases": "route.knowledgeBaseList"
};

// 兼容历史动态路由：优先使用 meta.titleKey，以下仅保留少量中文标题兜底映射。
const titleKeyByTitle: Record<string, string> = {
  "首页": "route.home",
  "工作台": "route.workspace",
  "页面未找到": "route.notFound",
  "通知中心": "route.notifications",
  "字典管理": "route.dictTypes",
  "参数配置": "route.systemConfigs",
  "数据源管理": "route.datasources",
  "告警": "route.alertLegacy",
  "告警管理": "route.alert",
  "审计日志": "route.audit"
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
  const product = translate("documentTitle.productName");
  document.title = title
    ? translate("documentTitle.pageWithProduct", { page: title, product })
    : product;
}

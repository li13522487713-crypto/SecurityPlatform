import type { RouteRecordRaw } from "vue-router";
import type { RouterVo } from "@/types/api";

const pageModules = import.meta.glob("../pages/**/*.vue");

const pathComponentFallbackMap: Record<string, string> = {
  "/": "../pages/HomePage.vue",
  "/console": "../pages/console/ConsolePage.vue",
  "/console/apps": "../pages/console/ConsolePage.vue",
  "/console/catalog": "../pages/console/ApplicationCatalogPage.vue",
  "/console/tenant-applications": "../pages/console/TenantApplicationsPage.vue",
  "/console/runtime-contexts": "../pages/console/RuntimeContextsPage.vue",
  "/console/runtime-executions": "../pages/console/RuntimeExecutionsPage.vue",
  "/console/resources": "../pages/console/ResourceCenterPage.vue",
  "/console/resources/datasource-consumption": "../pages/console/DataSourceConsumptionPage.vue",
  "/console/releases": "../pages/console/ReleaseCenterPage.vue",
  "/console/debug": "../pages/console/CozeDebugPage.vue",
  "/console/migration-governance": "../pages/console/MigrationGovernancePage.vue",
  "/console/app-db-migrations": "../pages/console/AppDatabaseMigrationPage.vue",
  "/console/datasources": "../pages/system/TenantDataSourcesPage.vue",
  "/console/settings/system/configs": "../pages/system/SystemConfigsPage.vue",
  "/apps/:appId/dashboard": "../pages/apps/AppDashboardPage.vue",
  "/apps/:appId/settings": "../pages/apps/AppSettingsPage.vue",
  "/apps/:appId/builder": "../pages/lowcode/AppBuilderPage.vue",
  "/apps/:appId/forms/:id/designer": "../pages/lowcode/FormDesignerPage.vue",
  "/apps/:appId/agents": "../pages/ai/AgentListPage.vue",
  "/apps/:appId/multi-agent": "../pages/ai/multi-agent/MultiAgentOrchestrationListPage.vue",
  "/apps/:appId/multi-agent/:id": "../pages/ai/multi-agent/MultiAgentOrchestrationDetailPage.vue",
  "/apps/:appId/evaluations/datasets": "../pages/ai/AiTestSetsPage.vue",
  "/apps/:appId/evaluations/tasks": "../pages/ai/EvaluationTaskPage.vue",
  "/apps/:appId/evaluations/reports/:taskId": "../pages/ai/EvaluationReportPage.vue",
  "/apps/:appId/memories": "../pages/ai/UserMemorySettingsPage.vue",
  "/apps/:appId/knowledge-bases": "../pages/ai/KnowledgeBaseListPage.vue",
  "/apps/:appId/knowledge-bases/:id": "../pages/ai/KnowledgeBaseDetailPage.vue",
  "/apps/:appId/knowledge-bases/:id/test": "../pages/ai/KnowledgeBaseTestPage.vue",
  "/apps/:appId/workflows": "../pages/workflow/WorkflowListPage.vue",
  "/apps/:appId/prompts": "../pages/ai/AiPromptLibraryPage.vue",
  "/apps/:appId/plugins": "../pages/ai/AiPluginListPage.vue",
  "/apps/:appId/plugins/:id": "../pages/ai/AiPluginDetailPage.vue",
  "/apps/:appId/plugins/:id/apis/:apiId": "../pages/ai/AiPluginApiEditorPage.vue",
  "/apps/:appId/workflows/:id/editor": "../pages/workflow/WorkflowEditorPage.vue",
  "/apps/:appId/agents/:id/edit": "../pages/ai/AgentEditorPage.vue",
  "/apps/:appId/data/:tableKey": "../pages/dynamic/DynamicTableCrudPage.vue",
  "/apps/:appId/run/:pageKey": "../pages/runtime/PageRuntimeRenderer.vue",
  "/r/:appKey/:pageKey": "../pages/runtime/PageRuntimeRenderer.vue",
  "/approval/flows": "../pages/ApprovalFlowsPage.vue",
  // 审批流程（/process/* 为旧路径，仍保留 fallback 以兼容历史菜单配置，新菜单应使用 /approval/* 路径）
  "/process/start": "../pages/ApprovalStartPage.vue",
  "/process/inbox": "../pages/ApprovalWorkspacePage.vue",
  "/process/done": "../pages/ApprovalWorkspacePage.vue",
  "/process/my-requests": "../pages/ApprovalWorkspacePage.vue",
  "/process/cc": "../pages/ApprovalWorkspacePage.vue",
  "/process/manage/flows": "../pages/ApprovalFlowManagePage.vue",
  "/process/manage/instances": "../pages/ApprovalInstanceManagePage.vue",
  "/assets": "../pages/AssetsPage.vue",
  "/audit": "../pages/AuditPage.vue",
  "/alert": "../pages/AlertPage.vue",
  "/alerts": "../pages/AlertPage.vue",
  "/workflow/designer": "../pages/WorkflowDesignerPage.vue",
  "/settings/org/users": "../pages/system/UsersPage.vue",
  "/settings/org/departments": "../pages/system/DepartmentsPage.vue",
  "/settings/org/positions": "../pages/system/PositionsPage.vue",
  "/settings/auth/roles": "../pages/system/RolesPage.vue",
  "/settings/auth/menus": "../pages/system/MenusPage.vue",
  "/settings/auth/pats": "../pages/settings/PersonalAccessTokensPage.vue",
  "/settings/projects": "../pages/system/ProjectsPage.vue",
  "/settings/system/datasources": "../pages/system/TenantDataSourcesPage.vue",
  "/settings/system/dict-types": "../pages/system/DictTypesPage.vue",
  "/settings/system/configs": "../pages/system/SystemConfigsPage.vue",
  "/admin/ai-config": "../pages/admin/AiConfigPage.vue",
  "/system/dict-types": "../pages/system/DictTypesPage.vue",
  "/system/configs": "../pages/system/SystemConfigsPage.vue",
  "/settings/ai/model-configs": "../pages/ai/ModelConfigsPage.vue",
  "/ai/agents": "../pages/ai/AgentListPage.vue",
  "/ai/multi-agent": "../pages/ai/multi-agent/MultiAgentOrchestrationListPage.vue",
  "/ai/multi-agent/:id": "../pages/ai/multi-agent/MultiAgentOrchestrationDetailPage.vue",
  "/ai/devops/test-sets": "../pages/ai/AiTestSetsPage.vue",
  "/ai/devops/evaluations/tasks": "../pages/ai/EvaluationTaskPage.vue",
  "/ai/devops/evaluations/reports/:taskId": "../pages/ai/EvaluationReportPage.vue",
  "/ai/agents/:id/edit": "../pages/ai/AgentEditorPage.vue",
  "/ai/agents/:agentId/chat": "../pages/ai/AgentChatPage.vue",
  "/ai/memories": "../pages/ai/UserMemorySettingsPage.vue",
  "/ai/knowledge-bases": "../pages/ai/KnowledgeBaseListPage.vue",
  "/ai/knowledge-bases/:id": "../pages/ai/KnowledgeBaseDetailPage.vue",
  "/ai/knowledge-bases/:id/test": "../pages/ai/KnowledgeBaseTestPage.vue",
  "/ai/workflows": "../pages/ai/AiWorkflowListPage.vue",
  "/ai/workflows/:id/edit": "../pages/ai/AiWorkflowEditorPage.vue",
  // 可视化流程管理
  "/visualization": "../pages/visualization/VisualizationCenterPage.vue",
  "/visualization/designer": "../pages/visualization/VisualizationDesignerPage.vue",
  "/visualization/designer/:id": "../pages/visualization/VisualizationDesignerPage.vue",
  "/visualization/runtime": "../pages/visualization/VisualizationRuntimePage.vue",
  "/visualization/governance": "../pages/visualization/VisualizationGovernancePage.vue",
  // 低代码辅助功能
  "/lowcode/process-monitor": "../pages/lowcode/ProcessMonitorPage.vue",
  "/lowcode/messages": "../pages/lowcode/MessageCenterPage.vue",
  "/lowcode/ai-assistant": "../pages/lowcode/AiAssistantPage.vue",
  // 审批管理（代理/部门负责人/任务池）
  "/process/agent-config": "../pages/ApprovalAgentConfigPage.vue",
  "/process/department-leaders": "../pages/ApprovalDepartmentLeaderPage.vue",
  "/process/task-pool": "../pages/ApprovalTaskPoolPage.vue",
  // 监控运维补全
  "/monitor/server-info": "../pages/monitor/ServerInfoPage.vue",
  "/monitor/scheduled-jobs": "../pages/monitor/ScheduledJobsPage.vue",
  "/system/login-logs": "../pages/system/LoginLogsPage.vue",
  "/system/online-users": "../pages/system/OnlineUsersPage.vue",
  "/monitor/outbox": "../pages/monitor/OutboxMonitorPage.vue",
  // License & 模板市场
  "/settings/license": "../pages/LicensePage.vue",
  "/lowcode/templates": "../pages/lowcode/TemplateMarketPage.vue",
  // 低代码管理扩展
  "/lowcode/migrations": "../pages/lowcode/DynamicMigrationsPage.vue",
  "/lowcode/packages": "../pages/lowcode/PackagesPage.vue",
  // 集成连接器
  "/settings/system/connectors": "../pages/system/ApiConnectorsPage.vue",
  // 报表和仪表盘
  "/lowcode/reports": "../pages/lowcode/ReportsPage.vue",
  "/lowcode/dashboards": "../pages/lowcode/DashboardsPage.vue"
};

const pathTitleKeyFallbackMap: Record<string, string> = {
  "/": "route.home",
  "/index": "route.workspace",
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
  "/console/datasources": "route.consoleDatasources",
  "/console/settings/system/configs": "route.consoleSystemConfigs",
  "/console/migration-governance": "route.consoleMigrationGovernance",
  "/console/app-db-migrations": "route.consoleAppDbMigrations",
  "/apps/:appId/dashboard": "route.appDashboard",
  "/apps/:appId/settings": "route.appSettings",
  "/apps/:appId/builder": "route.appBuilder",
  "/apps/:appId/forms/:id/designer": "route.formDesigner",
  "/apps/:appId/agents": "route.aiAgentList",
  "/apps/:appId/multi-agent": "route.aiMultiAgentList",
  "/apps/:appId/multi-agent/:id": "route.aiMultiAgentDetail",
  "/apps/:appId/evaluations/datasets": "route.aiTestSets",
  "/apps/:appId/evaluations/tasks": "route.aiEvaluationTasks",
  "/apps/:appId/evaluations/reports/:taskId": "route.aiEvaluationReport",
  "/apps/:appId/memories": "route.aiMemorySettings",
  "/apps/:appId/knowledge-bases": "route.knowledgeBaseList",
  "/apps/:appId/knowledge-bases/:id": "route.knowledgeBaseDetail",
  "/apps/:appId/knowledge-bases/:id/test": "route.knowledgeBaseTest",
  "/apps/:appId/workflows": "route.aiWorkflowList",
  "/apps/:appId/prompts": "route.aiPromptTemplates",
  "/apps/:appId/plugins": "route.aiPluginConfig",
  "/apps/:appId/plugins/:id": "route.aiPluginDetail",
  "/apps/:appId/plugins/:id/apis/:apiId": "route.aiPluginApiEditor",
  "/apps/:appId/workflows/:id/editor": "route.workflowEditor",
  "/apps/:appId/agents/:id/edit": "route.aiAgentEdit",
  "/apps/:appId/data/:tableKey": "route.dataManage",
  "/apps/:appId/run/:pageKey": "route.appRuntime",
  "/r/:appKey/:pageKey": "route.runtimeDelivery",
  "/approval/flows": "route.approvalFlows",
  "/process/start": "route.approvalStart",
  "/process/inbox": "route.approvalInbox",
  "/process/done": "route.approvalDone",
  "/process/my-requests": "route.approvalMyRequests",
  "/process/cc": "route.approvalCc",
  "/process/manage/flows": "route.approvalFlowManage",
  "/process/manage/instances": "route.approvalInstancesManage",
  "/assets": "route.assets",
  "/audit": "route.audit",
  "/alert": "route.alert",
  "/alerts": "route.alert",
  "/workflow/designer": "route.workflowDesigner",
  "/settings/org/users": "route.users",
  "/settings/org/departments": "route.departments",
  "/settings/org/positions": "route.positions",
  "/settings/auth/roles": "route.roles",
  "/settings/auth/menus": "route.menus",
  "/settings/auth/pats": "route.personalAccessTokens",
  "/settings/projects": "route.projects",
  "/settings/system/datasources": "route.datasources",
  "/settings/system/dict-types": "route.dictTypes",
  "/settings/system/configs": "route.systemConfigs",
  "/admin/ai-config": "route.aiAdminConfig",
  "/system/dict-types": "route.dictTypes",
  "/system/configs": "route.systemConfigs",
  "/settings/ai/model-configs": "route.modelConfigs",
  "/ai/agents": "route.aiAgentList",
  "/ai/multi-agent": "route.aiMultiAgentList",
  "/ai/multi-agent/:id": "route.aiMultiAgentDetail",
  "/ai/devops/test-sets": "route.aiTestSets",
  "/ai/devops/evaluations/tasks": "route.aiEvaluationTasks",
  "/ai/devops/evaluations/reports/:taskId": "route.aiEvaluationReport",
  "/ai/agents/:id/edit": "route.aiAgentEdit",
  "/ai/agents/:agentId/chat": "route.agentChat",
  "/ai/memories": "route.aiMemorySettings",
  "/ai/knowledge-bases": "route.knowledgeBaseList",
  "/ai/knowledge-bases/:id": "route.knowledgeBaseDetail",
  "/ai/knowledge-bases/:id/test": "route.knowledgeBaseTest",
  "/ai/workflows": "route.aiWorkflowListGlobal",
  "/ai/workflows/:id/edit": "route.aiWorkflowEditor",
  "/visualization": "route.visualizationCenter",
  "/visualization/designer": "route.visualizationDesigner",
  "/visualization/designer/:id": "route.visualizationDesigner",
  "/visualization/runtime": "route.visualizationRuntime",
  "/visualization/governance": "route.visualizationGovernance",
  "/lowcode/process-monitor": "route.processMonitor",
  "/lowcode/messages": "route.messageCenter",
  "/lowcode/ai-assistant": "route.aiAssistant",
  "/process/agent-config": "route.approvalAgentConfig",
  "/process/department-leaders": "route.approvalDepartmentLeaders",
  "/process/task-pool": "route.approvalTaskPool",
  "/monitor/server-info": "route.serverInfo",
  "/monitor/scheduled-jobs": "route.scheduledJobs",
  "/system/login-logs": "route.loginLogs",
  "/system/online-users": "route.onlineUsers",
  "/monitor/outbox": "route.outboxMonitor",
  "/settings/license": "route.license",
  "/lowcode/templates": "route.templateMarket",
  "/lowcode/migrations": "route.dynamicMigrations",
  "/lowcode/packages": "route.packages",
  "/settings/system/connectors": "route.apiConnectors",
  "/lowcode/reports": "route.reports",
  "/lowcode/dashboards": "route.dashboards"
};

function resolveByPathFallback(path?: string) {
  if (!path) {
    return null;
  }

  const modulePath = pathComponentFallbackMap[path];
  if (!modulePath) {
    return null;
  }

  return pageModules[modulePath] ?? null;
}

function normalizeRoutePath(path: string): string {
  if (!path) {
    return "";
  }

  if (path === "/") {
    return "/";
  }

  const normalized = path.replace(/\/{2,}/g, "/");
  if (normalized.startsWith("/")) {
    return normalized;
  }

  return `/${normalized}`;
}

function joinRoutePath(parentPath: string, childPath: string): string {
  if (!childPath) {
    return normalizeRoutePath(parentPath);
  }

  if (childPath.startsWith("/")) {
    return normalizeRoutePath(childPath);
  }

  const base = parentPath.endsWith("/") ? parentPath.slice(0, -1) : parentPath;
  return normalizeRoutePath(`${base}/${childPath}`);
}

function resolveComponent(component?: string, path?: string) {
  if (!component) {
    const fallback = resolveByPathFallback(path);
    if (fallback) {
      return fallback;
    }
    return () => import("@/pages/NotFoundPage.vue");
  }

  if (component === "Layout" || component === "ParentView") {
    return () => import("@/components/layout/RouterContainer.vue");
  }

  const candidates = [
    `../pages/${component}.vue`,
    `../pages/${component}/index.vue`
  ];
  for (const c of candidates) {
    if (pageModules[c]) return pageModules[c];
  }

  const fallback = resolveByPathFallback(path);
  if (fallback) {
    return fallback;
  }

  return () => import("@/pages/NotFoundPage.vue");
}

export function buildRoutesFromRouters(
  routers: RouterVo[],
  lastRouter: RouterVo | false = false,
  type = false
): RouteRecordRaw[] {
  return routers
    .filter((item) => {
      if (type && item.children) {
        item.children = filterChildren(item.children, item);
      }
      return item.path && item.name;
    })
    .map((item) => toRouteRecord(item, type))
    .filter((item): item is RouteRecordRaw => !!item);
}

function filterChildren(childrenMap: RouterVo[], lastRouter: RouterVo | false = false): RouterVo[] {
  let children: RouterVo[] = [];
  childrenMap.forEach((el) => {
    if (el.children && el.children.length) {
      if (el.component === "ParentView") {
        el.children.forEach((c) => {
          c.path = joinRoutePath(el.path, c.path);
          if (c.children && c.children.length) {
            children = children.concat(filterChildren(c.children, c));
            return;
          }
          children.push(c);
        });
        return;
      }
    }
    if (lastRouter) {
      el.path = joinRoutePath(lastRouter.path, el.path);
    }
    children = children.concat(el);
  });
  return children;
}

function toRouteRecord(item: RouterVo, type: boolean): RouteRecordRaw | null {
  const children = item.children && item.children.length > 0
    ? buildRoutesFromRouters(item.children, item, type)
    : undefined;

  const baseMeta = {
    title: item.meta?.title ?? item.name,
    titleKey: item.meta?.titleKey || pathTitleKeyFallbackMap[item.path],
    icon: item.meta?.icon,
    requiresAuth: true,
    requiresPermission: item.meta?.permi,
    hidden: item.hidden,
    noCache: item.meta?.noCache,
    breadcrumb: item.meta?.link ? false : true
  };

  const route: RouteRecordRaw = children
    ? {
      path: item.path,
      name: item.name,
      component: resolveComponent(item.component, item.path),
      meta: baseMeta,
      children
    }
    : {
      path: item.path,
      name: item.name,
      component: resolveComponent(item.component, item.path),
      meta: baseMeta
    };

  if (item.redirect) {
    (route as { redirect?: string }).redirect = item.redirect;
  }

  return route;
}

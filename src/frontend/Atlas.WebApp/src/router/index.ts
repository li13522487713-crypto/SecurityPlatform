import { createRouter, createWebHistory } from "vue-router";
import { getAccessToken, getTenantId } from "@/utils/auth";
import { getCurrentAppIdFromStorage } from "@/utils/app-context";
import { useUserStore } from "@/stores/user";
import { usePermissionStore } from "@/stores/permission";
import { message } from "ant-design-vue";
import NProgress from "nprogress";
import "nprogress/nprogress.css";
import { applyDocumentTitle } from "@/utils/i18n-navigation";

NProgress.configure({ showSpinner: false });

interface ApiRequestErrorLike extends Error {
  status?: number;
  payload?: unknown;
}

const LoginPage = () => import("@/pages/LoginPage.vue");
const RegisterPage = () => import("@/pages/RegisterPage.vue");
const ProfilePage = () => import("@/pages/ProfilePage.vue");
const NotFoundPage = () => import("@/pages/NotFoundPage.vue");
const ConsolePage = () => import("@/pages/console/ConsolePage.vue");
const AppDashboardPage = () => import("@/pages/apps/AppDashboardPage.vue");
const AppSettingsPage = () => import("@/pages/apps/AppSettingsPage.vue");
const AppPagesPage = () => import("@/pages/apps/AppPagesPage.vue");
const AppFormsPage = () => import("@/pages/lowcode/FormListPage.vue");
const AppFlowsPage = () => import("@/pages/ApprovalFlowsPage.vue");
const AppDataPage = () => import("@/pages/dynamic/DynamicTablesPage.vue");
const AppPermissionsPage = () => import("@/pages/system/PermissionsPage.vue");
const ModelConfigsPage = () => import("@/pages/ai/ModelConfigsPage.vue");
const AiVariablesPage = () => import("@/pages/ai/AiVariablesPage.vue");
const AiOpenPlatformPage = () => import("@/pages/ai/AiOpenPlatformPage.vue");
const AiWorkspacePage = () => import("@/pages/ai/AiWorkspacePage.vue");
const AiLibraryPage = () => import("@/pages/ai/AiLibraryPage.vue");
const AiTestSetsPage = () => import("@/pages/ai/AiTestSetsPage.vue");
const AiMockSetsPage = () => import("@/pages/ai/AiMockSetsPage.vue");
const AiShortcutsPage = () => import("@/pages/ai/AiShortcutsPage.vue");
const AiSearchResultsPage = () => import("@/pages/ai/AiSearchResultsPage.vue");
const AiMarketplacePage = () => import("@/pages/ai/AiMarketplacePage.vue");
const AgentEditorPage = () => import("@/pages/ai/AgentEditorPage.vue");
const PageRuntimeRenderer = () => import("@/pages/runtime/PageRuntimeRenderer.vue");
const AppListPage = () => import("@/pages/lowcode/AppListPage.vue");
const AppBuilderPage = () => import("@/pages/lowcode/AppBuilderPage.vue");
const FormListPage = () => import("@/pages/lowcode/FormListPage.vue");
const FormDesignerPage = () => import("@/pages/lowcode/FormDesignerPage.vue");
const WritebackMonitorPage = () => import("@/pages/lowcode/WritebackMonitorPage.vue");
const TemplateMarketPage = () => import("@/pages/lowcode/TemplateMarketPage.vue");
const ApprovalInstanceDetailPage = () => import("@/pages/ApprovalInstanceDetailPage.vue");
const NotificationsPage = () => import("@/pages/system/NotificationsPage.vue");
const DictTypesPage = () => import("@/pages/system/DictTypesPage.vue");
const SystemConfigsPage = () => import("@/pages/system/SystemConfigsPage.vue");
const TenantDataSourcesPage = () => import("@/pages/system/TenantDataSourcesPage.vue");
const TenantsPage = () => import("@/pages/system/TenantsPage.vue");
const RolesPage = () => import("@/pages/system/RolesPage.vue");
const PluginMarketPage = () => import("@/pages/lowcode/PluginMarketPage.vue");
const PluginManagePage = () => import("@/pages/system/PluginManagePage.vue");
const WebhooksPage = () => import("@/pages/system/WebhooksPage.vue");
const MessageQueuePage = () => import("@/pages/monitor/MessageQueuePage.vue");
const ServerInfoPage = () => import("@/pages/monitor/ServerInfoPage.vue");
const ScheduledJobsPage = () => import("@/pages/monitor/ScheduledJobsPage.vue");
const LoginLogsPage = () => import("@/pages/system/LoginLogsPage.vue");
const OnlineUsersPage = () => import("@/pages/system/OnlineUsersPage.vue");
const NotificationManagePage = () => import("@/pages/system/NotificationManagePage.vue");
const LicensePage = () => import("@/pages/LicensePage.vue");
const ToolsAuthorizationPage = () => import("@/pages/console/ToolsAuthorizationPage.vue");
const WorkflowListPage = () => import("@/pages/workflow/WorkflowListPage.vue");
const WorkflowEditorPage = () => import("@/pages/workflow/WorkflowEditorPage.vue");
const ApprovalDesignerPage = () => import("@/pages/ApprovalDesignerPage.vue");
const ApprovalFlowManagePage = () => import("@/pages/ApprovalFlowManagePage.vue");
const ApprovalFlowsPage = () => import("@/pages/ApprovalFlowsPage.vue");
const ApprovalInstanceManagePage = () => import("@/pages/ApprovalInstanceManagePage.vue");
const ApprovalWorkspacePage = () => import("@/pages/ApprovalWorkspacePage.vue");
const DepartmentsPage = () => import("@/pages/system/DepartmentsPage.vue");
const PositionsPage = () => import("@/pages/system/PositionsPage.vue");
const UsersPage = () => import("@/pages/system/UsersPage.vue");
const MenusPage = () => import("@/pages/system/MenusPage.vue");
const ProjectsPage = () => import("@/pages/system/ProjectsPage.vue");
const AssetsPage = () => import("@/pages/AssetsPage.vue");
const AuditPage = () => import("@/pages/AuditPage.vue");
const AlertPage = () => import("@/pages/AlertPage.vue");
declare module "vue-router" {
  interface RouteMeta {
    requiresAuth?: boolean;
    requiresPermission?: string;
    title?: string;
    titleKey?: string;
  }
}

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: "/login", name: "login", component: LoginPage, meta: { title: "登录" } },
    { path: "/register", name: "register", component: RegisterPage, meta: { title: "注册" } },
    { path: "/profile", name: "profile", component: ProfilePage, meta: { requiresAuth: true, title: "个人中心" } },
    { path: "/console", name: "console-home", component: ConsolePage, meta: { requiresAuth: true, title: "平台控制台", requiresPermission: "apps:view" } },
    { path: "/console/apps", name: "console-apps", component: ConsolePage, meta: { requiresAuth: true, title: "应用中心", requiresPermission: "apps:view" } },
    { path: "/console/resources", name: "console-resources", component: ConsolePage, meta: { requiresAuth: true, title: "资源中心", requiresPermission: "apps:view" } },
    { path: "/console/releases", name: "console-releases", component: ConsolePage, meta: { requiresAuth: true, title: "发布中心", requiresPermission: "apps:view" } },
    { path: "/console/tools", name: "console-tools", component: ToolsAuthorizationPage, meta: { requiresAuth: true, title: "工具授权中心", requiresPermission: "system:admin" } },
    { path: "/console/datasources", name: "console-datasources", component: TenantDataSourcesPage, meta: { requiresAuth: true, title: "数据源管理", requiresPermission: "system:admin" } },
    { path: "/console/settings/system/configs", name: "console-system-configs", component: SystemConfigsPage, meta: { requiresAuth: true, title: "系统设置", requiresPermission: "config:view" } },
    { path: "/apps/:appId", name: "app-workspace-root", redirect: to => `/apps/${to.params.appId}/dashboard`, meta: { requiresAuth: true, title: "应用工作台", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/dashboard", name: "app-workspace-dashboard", component: AppDashboardPage, meta: { requiresAuth: true, title: "应用仪表盘", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/builder", name: "app-workspace-builder", component: AppBuilderPage, meta: { requiresAuth: true, title: "应用设计器", requiresPermission: "apps:update" } },
    { path: "/apps/:appId/settings", name: "app-workspace-settings", component: AppSettingsPage, meta: { requiresAuth: true, title: "应用设置", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/pages", name: "app-workspace-pages", component: AppPagesPage, meta: { requiresAuth: true, title: "页面管理", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/forms", name: "app-workspace-forms", component: AppFormsPage, meta: { requiresAuth: true, title: "表单管理", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/forms/:id/designer", name: "app-workspace-form-designer", component: FormDesignerPage, meta: { requiresAuth: true, title: "表单设计器", requiresPermission: "apps:update" } },
    { path: "/apps/:appId/flows", name: "app-workspace-flows", component: AppFlowsPage, meta: { requiresAuth: true, title: "流程管理", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/workflows/:id/editor", name: "app-workspace-workflow-editor", component: WorkflowEditorPage, meta: { requiresAuth: true, title: "工作流设计器" } },
    { path: "/apps/:appId/agents/:id/edit", name: "app-workspace-agent-editor", component: AgentEditorPage, meta: { requiresAuth: true, title: "Agent 编辑" } },
    { path: "/apps/:appId/data", name: "app-workspace-data", component: AppDataPage, meta: { requiresAuth: true, title: "数据管理", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/permissions", name: "app-workspace-permissions", component: AppPermissionsPage, meta: { requiresAuth: true, title: "权限入口", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/run/:pageKey", name: "app-workspace-runtime", component: PageRuntimeRenderer, meta: { requiresAuth: true, title: "应用运行态", requiresPermission: "apps:view" } },
    { path: "/r/:appKey/:pageKey", name: "runtime-delivery-page", component: PageRuntimeRenderer, meta: { requiresAuth: true, title: "运行交付面" } },
    { path: "/process/instances/:id", name: "process-instance-detail", component: ApprovalInstanceDetailPage, meta: { requiresAuth: true, title: "流程详情", requiresPermission: "approval:flow:view" } },
    { path: "/system/notifications", name: "system-notifications", component: NotificationsPage, meta: { requiresAuth: true, title: "通知中心" } },
    { path: "/system/notifications/manage", name: "system-notifications-manage", component: NotificationManagePage, meta: { requiresAuth: true, title: "公告管理", requiresPermission: "notification:manage" } },
    { path: "/notifications", name: "system-notifications-legacy", redirect: "/system/notifications", meta: { requiresAuth: true, title: "通知中心" } },
    { path: "/settings/system/dict-types", name: "settings-system-dict-types", component: DictTypesPage, meta: { requiresAuth: true, title: "字典管理", requiresPermission: "dict:type:view" } },
    { path: "/settings/system/datasources", name: "settings-system-datasources", component: TenantDataSourcesPage, meta: { requiresAuth: true, title: "数据源管理", requiresPermission: "system:admin" } },
    { path: "/settings/system/configs", name: "settings-system-configs", component: SystemConfigsPage, meta: { requiresAuth: true, title: "参数配置", requiresPermission: "config:view" } },
    { path: "/settings/ai/model-configs", name: "settings-ai-model-configs", component: ModelConfigsPage, meta: { requiresAuth: true, title: "模型配置" } },
    { path: "/ai/variables", name: "ai-variables-static", component: AiVariablesPage, meta: { requiresAuth: true, title: "变量管理" } },
    { path: "/ai/open-platform", name: "ai-open-platform-static", component: AiOpenPlatformPage, meta: { requiresAuth: true, title: "开放平台" } },
    { path: "/ai/workspace", name: "ai-workspace-static", component: AiWorkspacePage, meta: { requiresAuth: true, title: "AI 工作台" } },
    { path: "/ai/library", name: "ai-library-static", component: AiLibraryPage, meta: { requiresAuth: true, title: "资源库" } },
    { path: "/ai/devops/test-sets", name: "ai-test-sets-static", component: AiTestSetsPage, meta: { requiresAuth: true, title: "测试集" } },
    { path: "/ai/devops/mock-sets", name: "ai-mock-sets-static", component: AiMockSetsPage, meta: { requiresAuth: true, title: "Mock 集" } },
    { path: "/ai/shortcuts", name: "ai-shortcuts-static", component: AiShortcutsPage, meta: { requiresAuth: true, title: "快捷命令" } },
    { path: "/ai/search", name: "ai-search-static", component: AiSearchResultsPage, meta: { requiresAuth: true, title: "统一搜索" } },
    { path: "/ai/marketplace", name: "ai-marketplace-static", component: AiMarketplacePage, meta: { requiresAuth: true, title: "应用市场" } },
    {
      path: "/ai/agents/:id/edit",
      name: "ai-agent-edit-static",
      redirect: to => buildWorkspaceRedirectPath(to, appId => `/apps/${appId}/agents/${to.params.id}/edit`),
      meta: { requiresAuth: true, title: "Agent 编辑(Deprecated)" }
    },
    { path: "/settings/auth/roles", name: "SettingsAuthRoles", component: RolesPage, meta: { requiresAuth: true, title: "角色管理", requiresPermission: "roles:view" } },
    { path: "/lowcode/plugin-market", name: "plugin-market", component: PluginMarketPage, meta: { requiresAuth: true, title: "插件市场" } },
    { path: "/settings/system/plugins", name: "settings-plugins", component: PluginManagePage, meta: { requiresAuth: true, title: "插件管理", requiresPermission: "system:admin" } },
    { path: "/settings/system/webhooks", name: "settings-webhooks", component: WebhooksPage, meta: { requiresAuth: true, title: "Webhook 管理" } },
    { path: "/monitor/message-queue", name: "monitor-message-queue", component: MessageQueuePage, meta: { requiresAuth: true, title: "消息队列监控", requiresPermission: "system:admin" } },
    { path: "/monitor/server-info", name: "monitor-server-info", component: ServerInfoPage, meta: { requiresAuth: true, title: "服务器监控", requiresPermission: "system:admin" } },
    { path: "/monitor/scheduled-jobs", name: "monitor-scheduled-jobs", component: ScheduledJobsPage, meta: { requiresAuth: true, title: "定时任务" } },
    { path: "/system/login-logs", name: "system-login-logs", component: LoginLogsPage, meta: { requiresAuth: true, title: "登录日志", requiresPermission: "system:admin" } },
    { path: "/system/online-users", name: "system-online-users", component: OnlineUsersPage, meta: { requiresAuth: true, title: "在线用户" } },

    { path: "/settings/license", name: "settings-license", component: LicensePage, meta: { requiresAuth: true, title: "授权管理", requiresPermission: "system:license:view" } },
    {
      path: "/settings/:pathMatch(.*)*",
      name: "settings-legacy",
      redirect: (to) => {
        const pathMatch = to.params.pathMatch;
        const suffix = Array.isArray(pathMatch)
          ? pathMatch.join("/")
          : typeof pathMatch === "string"
            ? pathMatch
            : "";
        return `/console/settings/${suffix}`;
      },
      meta: { requiresAuth: true, title: "兼容设置路由（Deprecated）" }
    },
    { path: "/system/dict-types", name: "system-dict-types-legacy", redirect: "/settings/system/dict-types", meta: { requiresAuth: true, title: "字典管理" } },
    { path: "/system/configs", name: "system-configs-legacy", redirect: "/settings/system/configs", meta: { requiresAuth: true, title: "参数配置" } },
    { path: "/alerts", name: "alerts-legacy", redirect: "/alert", meta: { requiresAuth: true, title: "告警" } },
    { path: "/lowcode/apps", name: "app-list", component: AppListPage, meta: { requiresAuth: true, title: "低代码应用", requiresPermission: "apps:view" } },
    {
      path: "/lowcode/apps/:id/builder",
      name: "app-builder",
      redirect: to => `/apps/${to.params.id}/builder`,
      meta: { requiresAuth: true, title: "应用设计器(Deprecated)", requiresPermission: "apps:update" }
    },
    { path: "/lowcode/forms", name: "apps-form-list", component: FormListPage, meta: { requiresAuth: true, title: "表单管理", requiresPermission: "apps:view" } },
    { path: "/lowcode/templates", name: "template-market", component: TemplateMarketPage, meta: { requiresAuth: true, title: "模板市场", requiresPermission: "apps:view" } },
    {
      path: "/lowcode/forms/:id/designer",
      name: "apps-form-designer",
      redirect: to => buildWorkspaceRedirectPath(to, appId => `/apps/${appId}/forms/${to.params.id}/designer`),
      meta: { requiresAuth: true, title: "表单设计器(Deprecated)", requiresPermission: "apps:update" }
    },
    { path: "/monitor/writeback-failures", name: "monitor-writeback-failures", component: WritebackMonitorPage, meta: { requiresAuth: true, title: "回写监控", requiresPermission: "system:admin" } },
    { path: "/workflow", name: "workflow-list", component: WorkflowListPage, meta: { requiresAuth: true, title: "工作流管理" } },
    {
      path: "/workflow/:id/editor",
      name: "workflow-editor",
      redirect: to => buildWorkspaceRedirectPath(to, appId => `/apps/${appId}/workflows/${to.params.id}/editor`),
      meta: { requiresAuth: true, title: "工作流设计器(Deprecated)" }
    },
    {
      path: "/ai/workflows/:id/edit",
      name: "ai-workflow-editor-legacy",
      redirect: to => buildWorkspaceRedirectPath(to, appId => `/apps/${appId}/workflows/${to.params.id}/editor`),
      meta: { requiresAuth: true, title: "AI 工作流设计器(Deprecated)" }
    },
    { path: "/approval/designer", name: "approval-designer", component: ApprovalDesignerPage, meta: { requiresAuth: true, title: "流程设计器", requiresPermission: "approval:flow:create" } },
    { path: "/approval/flows/manage", name: "approval-flows-manage", component: ApprovalFlowManagePage, meta: { requiresAuth: true, title: "流程发布总览", requiresPermission: "approval:flow:manage" } },
    { path: "/approval/flows", name: "approval-flows", component: ApprovalFlowsPage, meta: { requiresAuth: true, title: "流程定义列表", requiresPermission: "approval:flow:view" } },
    { path: "/approval/instances/manage", name: "approval-instances-manage", component: ApprovalInstanceManagePage, meta: { requiresAuth: true, title: "所有审批实例", requiresPermission: "system:admin" } },
    { path: "/approval/workspace", name: "approval-workspace", component: ApprovalWorkspacePage, meta: { requiresAuth: true, title: "审批工作台" } },
    { path: "/approval/instances", name: "approval-instances", redirect: "/approval/workspace?tab=requests", meta: { title: "我的申请(Deprecated)" } },
    { path: "/approval/inbox", name: "approval-inbox", redirect: "/approval/workspace?tab=pending", meta: { title: "审批待办(Deprecated)" } },
    { path: "/settings/org/tenants", name: "settings-org-tenants", component: TenantsPage, meta: { requiresAuth: true, title: "租户管理", requiresPermission: "system:tenant:query" } },
    { path: "/settings/org/departments", name: "settings-org-deps", component: DepartmentsPage, meta: { requiresAuth: true, title: "组织架构", requiresPermission: "departments:view" } },
    { path: "/settings/org/positions", name: "settings-org-positions", component: PositionsPage, meta: { requiresAuth: true, title: "职位名称", requiresPermission: "positions:view" } },
    { path: "/settings/org/users", name: "settings-org-users", component: UsersPage, meta: { requiresAuth: true, title: "员工管理", requiresPermission: "users:view" } },
    { path: "/settings/auth/menus", name: "settings-auth-menus", component: MenusPage, meta: { requiresAuth: true, title: "菜单管理", requiresPermission: "menus:view" } },
    { path: "/settings/projects", name: "settings-projects", component: ProjectsPage, meta: { requiresAuth: true, title: "项目管理", requiresPermission: "projects:view" } },
    { path: "/assets", name: "assets-manage", component: AssetsPage, meta: { requiresAuth: true, title: "资产管理", requiresPermission: "assets:view" } },
    { path: "/audit", name: "audit-manage", component: AuditPage, meta: { requiresAuth: true, title: "审计日志", requiresPermission: "audit:view" } },
    { path: "/alert", name: "alert-manage", component: AlertPage, meta: { requiresAuth: true, title: "告警管理", requiresPermission: "alert:view" } },
    { path: "/approval/tasks", name: "approval-tasks", redirect: "/approval/workspace?tab=pending", meta: { title: "我的待办(Deprecated)" } },
    { path: "/approval/done", redirect: "/approval/workspace?tab=done", meta: { title: "已办任务(Deprecated)" } },
    { path: "/approval/cc", redirect: "/approval/workspace?tab=cc", meta: { title: "我的抄送(Deprecated)" } },
    { path: "/:pathMatch(.*)*", name: "not-found", component: NotFoundPage }
  ]
});

const whiteList = ["/login", "/register"];

function isPrivilegedUser(userStore: ReturnType<typeof useUserStore>) {
  return userStore.permissions.includes("*:*:*")
    || userStore.roles.some((role: string) => ["admin", "superadmin"].includes(role.toLowerCase()));
}

function hasPermission(userStore: ReturnType<typeof useUserStore>, permission: string) {
  return userStore.permissions.includes(permission) || isPrivilegedUser(userStore);
}

function getAuthFallbackPath(userStore: ReturnType<typeof useUserStore>) {
  if (hasPermission(userStore, "apps:view")) {
    return "/console";
  }
  if (hasPermission(userStore, "approval:flow:view")) {
    return "/approval/flows";
  }
  if (hasPermission(userStore, "users:view")) {
    return "/settings/org/users";
  }
  if (hasPermission(userStore, "audit:view")) {
    return "/audit";
  }
  return "/system/notifications";
}

function resolveLegacyAppId(to: { query: Record<string, unknown>; params: Record<string, unknown> }): string | null {
  const queryAppId = typeof to.query.appId === "string" ? to.query.appId : null;
  const paramAppId = typeof to.params.appId === "string" ? to.params.appId : null;
  const cachedAppId = getCurrentAppIdFromStorage();
  const selected = queryAppId || paramAppId || cachedAppId;
  if (!selected || !selected.trim()) {
    return null;
  }

  return selected.trim();
}

function buildWorkspaceRedirectPath(
  to: { query: Record<string, unknown>; params: Record<string, unknown> },
  suffixBuilder: (appId: string) => string
): string {
  const appId = resolveLegacyAppId(to);
  if (!appId) {
    return "/console/apps";
  }

  return suffixBuilder(appId);
}

router.beforeEach(async (to, from, next) => {
  NProgress.start();

  const token = getAccessToken();
  const tenantId = getTenantId();
  const userStore = useUserStore();
  const permissionStore = usePermissionStore();

  if (token && tenantId) {
    if (to.path === "/login") {
      next({ path: getAuthFallbackPath(userStore), replace: true });
      NProgress.done();
      return;
    }

    if (!permissionStore.routeLoaded || !userStore.profile) {
      try {
        await userStore.getInfo();
        await permissionStore.generateRoutes();
        permissionStore.registerRoutes(router);
        // 仅按 URL 重放，避免携带 not-found 名称导致动态路由注册后仍落入 404。
        next({
          path: to.path,
          query: to.query,
          hash: to.hash,
          replace: true
        });
        return;
      } catch (err) {
        console.error(err);
        await userStore.logout();
        const requestError = err as ApiRequestErrorLike;
        const alreadyHandledByApiCore = typeof requestError?.status === "number" || requestError?.payload !== undefined;
        if (!alreadyHandledByApiCore) {
          // 仅兜底提示非 API 异常，避免与 api-core 全局错误提示重复弹窗。
          message.error(requestError?.message || "登录失败，请重新登录");
        }
        next({ path: "/login" });
        NProgress.done();
        return;
      }
    }

    if (to.meta.requiresPermission && typeof to.meta.requiresPermission === "string") {
      const has = hasPermission(userStore, to.meta.requiresPermission);
      if (!has) {
        const fallbackPath = getAuthFallbackPath(userStore);
        if (fallbackPath !== to.path) {
          next({ path: fallbackPath, replace: true });
        } else {
          next({ path: "/system/notifications", replace: true });
        }
        NProgress.done();
        return;
      }
    }

    next();
  } else {
    if (whiteList.includes(to.path)) {
      next();
    } else {
      next(`/login?redirect=${encodeURIComponent(to.fullPath)}`);
      NProgress.done();
    }
  }
});

router.afterEach(() => {
  applyDocumentTitle(router.currentRoute.value);
  NProgress.done();
});

export default router;

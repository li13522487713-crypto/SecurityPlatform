import { createRouter, createWebHistory } from "vue-router";
import { getAccessToken, getTenantId } from "@/utils/auth";
import { useUserStore } from "@/stores/user";
import { usePermissionStore } from "@/stores/permission";
import { message } from "ant-design-vue";
import NProgress from "nprogress";
import "nprogress/nprogress.css";

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
const RolesPage = () => import("@/pages/system/RolesPage.vue");
const PluginMarketPage = () => import("@/pages/lowcode/PluginMarketPage.vue");
const PluginManagePage = () => import("@/pages/system/PluginManagePage.vue");
const WebhooksPage = () => import("@/pages/system/WebhooksPage.vue");
const MessageQueuePage = () => import("@/pages/monitor/MessageQueuePage.vue");
const LicensePage = () => import("@/pages/LicensePage.vue");
const ToolsAuthorizationPage = () => import("@/pages/console/ToolsAuthorizationPage.vue");

declare module "vue-router" {
  interface RouteMeta {
    requiresAuth?: boolean;
    requiresPermission?: string;
    title?: string;
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
    { path: "/apps/:appId/flows", name: "app-workspace-flows", component: AppFlowsPage, meta: { requiresAuth: true, title: "流程管理", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/data", name: "app-workspace-data", component: AppDataPage, meta: { requiresAuth: true, title: "数据管理", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/permissions", name: "app-workspace-permissions", component: AppPermissionsPage, meta: { requiresAuth: true, title: "权限入口", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/run/:pageKey", name: "app-workspace-runtime", component: PageRuntimeRenderer, meta: { requiresAuth: true, title: "应用运行态", requiresPermission: "apps:view" } },
    { path: "/r/:appKey/:pageKey", name: "runtime-delivery-page", component: PageRuntimeRenderer, meta: { requiresAuth: true, title: "运行交付面" } },
    { path: "/process/instances/:id", name: "process-instance-detail", component: ApprovalInstanceDetailPage, meta: { requiresAuth: true, title: "流程详情", requiresPermission: "approval:flow:view" } },
    { path: "/system/notifications", name: "system-notifications", component: NotificationsPage, meta: { requiresAuth: true, title: "通知中心" } },
    { path: "/notifications", name: "system-notifications-legacy", redirect: "/system/notifications", meta: { requiresAuth: true, title: "通知中心" } },
    { path: "/settings/system/dict-types", name: "settings-system-dict-types", component: DictTypesPage, meta: { requiresAuth: true, title: "字典管理", requiresPermission: "dict:type:view" } },
    { path: "/settings/system/configs", name: "settings-system-configs", component: SystemConfigsPage, meta: { requiresAuth: true, title: "参数配置", requiresPermission: "config:view" } },
    { path: "/settings/auth/roles", name: "SettingsAuthRoles", component: RolesPage, meta: { requiresAuth: true, title: "角色管理", requiresPermission: "roles:view" } },
    { path: "/lowcode/plugin-market", name: "plugin-market", component: PluginMarketPage, meta: { requiresAuth: true, title: "插件市场" } },
    { path: "/settings/system/plugins", name: "settings-plugins", component: PluginManagePage, meta: { requiresAuth: true, title: "插件管理", requiresPermission: "system:admin" } },
    { path: "/settings/system/webhooks", name: "settings-webhooks", component: WebhooksPage, meta: { requiresAuth: true, title: "Webhook 管理" } },
    { path: "/monitor/message-queue", name: "monitor-message-queue", component: MessageQueuePage, meta: { requiresAuth: true, title: "消息队列监控", requiresPermission: "system:admin" } },
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
    { path: "/lowcode/apps/:id/builder", name: "app-builder", component: AppBuilderPage, meta: { requiresAuth: true, title: "应用设计器", requiresPermission: "apps:update" } },
    { path: "/lowcode/forms", name: "apps-form-list", component: FormListPage, meta: { requiresAuth: true, title: "表单管理", requiresPermission: "apps:view" } },
    { path: "/lowcode/templates", name: "template-market", component: TemplateMarketPage, meta: { requiresAuth: true, title: "模板市场", requiresPermission: "apps:view" } },
    { path: "/lowcode/forms/:id/designer", name: "apps-form-designer", component: FormDesignerPage, meta: { requiresAuth: true, title: "表单设计器", requiresPermission: "apps:update" } },
    { path: "/monitor/writeback-failures", name: "monitor-writeback-failures", component: WritebackMonitorPage, meta: { requiresAuth: true, title: "回写监控", requiresPermission: "system:admin" } },
    { path: "/:pathMatch(.*)*", name: "not-found", component: NotFoundPage }
  ]
});

const whiteList = ["/login", "/register"];

router.beforeEach(async (to, from, next) => {
  NProgress.start();
  if (to.meta.title) {
    document.title = `${to.meta.title} - Atlas Security Platform`;
  }

  const token = getAccessToken();
  const tenantId = getTenantId();
  const userStore = useUserStore();
  const permissionStore = usePermissionStore();

  if (token && tenantId) {
    if (to.path === "/login") {
      next({ path: "/console" });
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
      const has = userStore.permissions.includes(to.meta.requiresPermission)
        || userStore.permissions.includes("*:*:*")
        || userStore.roles.some((role: string) => ["admin", "superadmin"].includes(role.toLowerCase()));
      if (!has) {
        next({ path: "/console" });
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
  NProgress.done();
});

export default router;

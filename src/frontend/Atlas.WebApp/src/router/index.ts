import { createRouter, createWebHistory } from "vue-router";
import { getAccessToken, getTenantId } from "@/utils/auth";
import { useUserStore } from "@/stores/user";
import { usePermissionStore } from "@/stores/permission";
import { message } from "ant-design-vue";
import NProgress from "nprogress";
import "nprogress/nprogress.css";

NProgress.configure({ showSpinner: false });

const LoginPage = () => import("@/pages/LoginPage.vue");
const RegisterPage = () => import("@/pages/RegisterPage.vue");
const ProfilePage = () => import("@/pages/ProfilePage.vue");
const NotFoundPage = () => import("@/pages/NotFoundPage.vue");
const ConsoleLayout = () => import("@/layouts/ConsoleLayout.vue");
const AppWorkspaceLayout = () => import("@/layouts/AppWorkspaceLayout.vue");
const ConsolePage = () => import("@/pages/console/ConsolePage.vue");
const AppListPage = () => import("@/pages/lowcode/AppListPage.vue");
const AppBuilderPage = () => import("@/pages/lowcode/AppBuilderPage.vue");
const AppDashboardPage = () => import("@/pages/apps/AppDashboardPage.vue");
const AppDatasourcePage = () => import("@/pages/apps/settings/AppDatasourcePage.vue");
const AppSharingPolicyPage = () => import("@/pages/apps/settings/AppSharingPolicyPage.vue");
const AppEntityAliasPage = () => import("@/pages/apps/settings/AppEntityAliasPage.vue");
const FormListPage = () => import("@/pages/lowcode/FormListPage.vue");
const FormDesignerPage = () => import("@/pages/lowcode/FormDesignerPage.vue");
const WritebackMonitorPage = () => import("@/pages/lowcode/WritebackMonitorPage.vue");
const ApprovalInstanceDetailPage = () => import("@/pages/ApprovalInstanceDetailPage.vue");
const ApprovalFlowsPage = () => import("@/pages/ApprovalFlowsPage.vue");
const WorkflowDesignerPage = () => import("@/pages/WorkflowDesignerPage.vue");
const NotificationsPage = () => import("@/pages/system/NotificationsPage.vue");
const DictTypesPage = () => import("@/pages/system/DictTypesPage.vue");
const SystemConfigsPage = () => import("@/pages/system/SystemConfigsPage.vue");
const RolesPage = () => import("@/pages/system/RolesPage.vue");
const UsersPage = () => import("@/pages/system/UsersPage.vue");
const TenantDataSourcesPage = () => import("@/pages/system/TenantDataSourcesPage.vue");
const PluginMarketPage = () => import("@/pages/lowcode/PluginMarketPage.vue");
const PluginManagePage = () => import("@/pages/system/PluginManagePage.vue");
const WebhooksPage = () => import("@/pages/system/WebhooksPage.vue");
const MessageQueuePage = () => import("@/pages/monitor/MessageQueuePage.vue");

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
    {
      path: "/console",
      component: ConsoleLayout,
      children: [
        { path: "", name: "console-apps", component: ConsolePage, meta: { requiresAuth: true, title: "应用控制台", requiresPermission: "apps:view" } },
        { path: "datasources", name: "console-datasources", component: TenantDataSourcesPage, meta: { requiresAuth: true, title: "平台数据源", requiresPermission: "system:admin" } },
        { path: "settings/users", name: "console-settings-users", component: UsersPage, meta: { requiresAuth: true, title: "用户管理", requiresPermission: "users:view" } }
      ]
    },
    {
      path: "/apps/:appId",
      component: AppWorkspaceLayout,
      children: [
        { path: "dashboard", name: "app-dashboard", component: AppDashboardPage, meta: { requiresAuth: true, title: "应用仪表盘", requiresPermission: "apps:view" } },
        { path: "forms", name: "app-forms", component: FormListPage, meta: { requiresAuth: true, title: "表单管理", requiresPermission: "apps:view" } },
        { path: "builder", name: "app-builder-workspace", component: AppBuilderPage, meta: { requiresAuth: true, title: "低代码设计器", requiresPermission: "apps:update" } },
        { path: "approval", name: "app-approval", component: ApprovalFlowsPage, meta: { requiresAuth: true, title: "审批", requiresPermission: "approval:flow:view" } },
        { path: "workflow", name: "app-workflow", component: WorkflowDesignerPage, meta: { requiresAuth: true, title: "工作流", requiresPermission: "workflow:design" } },
        { path: "settings/datasource", name: "app-settings-datasource", component: AppDatasourcePage, meta: { requiresAuth: true, title: "数据源设置", requiresPermission: "apps:view" } },
        { path: "settings/sharing", name: "app-settings-sharing", component: AppSharingPolicyPage, meta: { requiresAuth: true, title: "共享策略设置", requiresPermission: "apps:update" } },
        { path: "settings/aliases", name: "app-settings-aliases", component: AppEntityAliasPage, meta: { requiresAuth: true, title: "实体别名设置", requiresPermission: "apps:update" } },
        { path: "", redirect: to => `/apps/${to.params.appId as string}/dashboard` }
      ]
    },
    { path: "/process/instances/:id", name: "process-instance-detail", component: ApprovalInstanceDetailPage, meta: { requiresAuth: true, title: "流程详情", requiresPermission: "approval:flow:view" } },
    { path: "/system/notifications", name: "system-notifications", component: NotificationsPage, meta: { requiresAuth: true, title: "通知中心" } },
    { path: "/notifications", name: "system-notifications-legacy", redirect: "/system/notifications", meta: { requiresAuth: true, title: "通知中心" } },
    { path: "/settings/system/dict-types", name: "settings-system-dict-types", component: DictTypesPage, meta: { requiresAuth: true, title: "字典管理", requiresPermission: "dict:type:view" } },
    { path: "/settings/system/configs", name: "settings-system-configs", component: SystemConfigsPage, meta: { requiresAuth: true, title: "参数配置", requiresPermission: "config:view" } },
    { path: "/settings/auth/roles", name: "SettingsAuthRoles", component: RolesPage, meta: { requiresAuth: true, title: "角色管理", requiresPermission: "roles:view" } },
    { path: "/lowcode/plugin-market", name: "plugin-market", component: PluginMarketPage, meta: { requiresAuth: true, title: "插件市场" } },
    { path: "/settings/system/plugins", name: "settings-plugins", component: PluginManagePage, meta: { requiresAuth: true, title: "插件管理", requiresPermission: "system:admin" } },
    { path: "/settings/system/webhooks", name: "settings-webhooks", component: WebhooksPage, meta: { requiresAuth: true, title: "Webhook 管理" } },
    { path: "/monitor/message-queue", name: "monitor-message-queue", component: MessageQueuePage, meta: { requiresAuth: true, title: "消息队列监控", requiresPermission: "system:admin" } },    { path: "/system/dict-types", name: "system-dict-types-legacy", redirect: "/settings/system/dict-types", meta: { requiresAuth: true, title: "字典管理" } },
    { path: "/system/configs", name: "system-configs-legacy", redirect: "/settings/system/configs", meta: { requiresAuth: true, title: "参数配置" } },
    { path: "/alerts", name: "alerts-legacy", redirect: "/alert", meta: { requiresAuth: true, title: "告警" } },
    { path: "/lowcode/apps", name: "app-list-legacy", redirect: "/console", meta: { requiresAuth: true, title: "低代码应用" } },
    { path: "/lowcode/apps/:id/builder", name: "app-builder-legacy", redirect: to => `/apps/${to.params.id as string}/builder`, meta: { requiresAuth: true, title: "应用设计器" } },
    { path: "/lowcode/apps-standalone", name: "app-list", component: AppListPage, meta: { requiresAuth: true, title: "低代码应用", requiresPermission: "apps:view" } },
    { path: "/lowcode/forms", name: "apps-form-list", component: FormListPage, meta: { requiresAuth: true, title: "表单管理", requiresPermission: "apps:view" } },
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
        message.error((err as Error)?.message || "登录失败，请重新登录");
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

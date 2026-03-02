import { createRouter, createWebHistory } from "vue-router";
import { getAccessToken, getAuthProfile, getTenantId, hasPermission } from "@/utils/auth";

// ─── Page imports ───────────────────────────────────────
const HomePage = () => import("@/pages/HomePage.vue");
const LoginPage = () => import("@/pages/LoginPage.vue");
const ProfilePage = () => import("@/pages/ProfilePage.vue");
const NotFoundPage = () => import("@/pages/NotFoundPage.vue");

// 安全中心
const AssetsPage = () => import("@/pages/AssetsPage.vue");
const AuditPage = () => import("@/pages/AuditPage.vue");
const AlertPage = () => import("@/pages/AlertPage.vue");

// 流程中心（合并审批中心 + 工作流引擎）
const ApprovalFlowsPage = () => import("@/pages/ApprovalFlowsPage.vue");
const ApprovalDesignerPage = () => import("@/pages/ApprovalDesignerPage.vue");
const ApprovalTasksPage = () => import("@/pages/ApprovalTasksPage.vue");
const ApprovalInstancesPage = () => import("@/pages/ApprovalInstancesPage.vue");
const ProcessMonitorPage = () => import("@/pages/lowcode/ProcessMonitorPage.vue");
const WorkflowDesignerPage = () => import("@/pages/WorkflowDesignerPage.vue");
const WorkflowInstancesPage = () => import("@/pages/WorkflowInstancesPage.vue");

// 应用中心（低代码平台）
const FormListPage = () => import("@/pages/lowcode/FormListPage.vue");
const FormDesignerPage = () => import("@/pages/lowcode/FormDesignerPage.vue");
const AppListPage = () => import("@/pages/lowcode/AppListPage.vue");
const AppBuilderPage = () => import("@/pages/lowcode/AppBuilderPage.vue");
const DynamicTablesPage = () => import("@/pages/dynamic/DynamicTablesPage.vue");
const DynamicTableCrudPage = () => import("@/pages/dynamic/DynamicTableCrudPage.vue");

// 系统设置
const UsersPage = () => import("@/pages/system/UsersPage.vue");
const RolesPage = () => import("@/pages/system/RolesPage.vue");
const PermissionsPage = () => import("@/pages/system/PermissionsPage.vue");
const MenusPage = () => import("@/pages/system/MenusPage.vue");
const DepartmentsPage = () => import("@/pages/system/DepartmentsPage.vue");
const PositionsPage = () => import("@/pages/system/PositionsPage.vue");
const AppsPage = () => import("@/pages/system/AppsPage.vue");
const ProjectsPage = () => import("@/pages/system/ProjectsPage.vue");
const AmisSystemPage = () => import("@/pages/system/AmisSystemPage.vue");
const DynamicTablesPage = () => import("@/pages/dynamic/DynamicTablesPage.vue");
const DynamicTableCrudPage = () => import("@/pages/dynamic/DynamicTableCrudPage.vue");
const NotFoundPage = () => import("@/pages/NotFoundPage.vue");
const DictTypesPage = () => import("@/pages/system/DictTypesPage.vue");
const SystemConfigsPage = () => import("@/pages/system/SystemConfigsPage.vue");
const LoginLogsPage = () => import("@/pages/system/LoginLogsPage.vue");
const OnlineUsersPage = () => import("@/pages/system/OnlineUsersPage.vue");
const NotificationsPage = () => import("@/pages/system/NotificationsPage.vue");
const ServerInfoPage = () => import("@/pages/monitor/ServerInfoPage.vue");
const ScheduledJobsPage = () => import("@/pages/monitor/ScheduledJobsPage.vue");

// 全局功能
const AiAssistantPage = () => import("@/pages/lowcode/AiAssistantPage.vue");

// 可视化（保留路由但从菜单移除）
const VisualizationCenterPage = () => import("@/pages/visualization/VisualizationCenterPage.vue");
const VisualizationDesignerPage = () => import("@/pages/visualization/VisualizationDesignerPage.vue");
const VisualizationRuntimePage = () => import("@/pages/visualization/VisualizationRuntimePage.vue");
const VisualizationGovernancePage = () => import("@/pages/visualization/VisualizationGovernancePage.vue");

// ─── Route meta types ───────────────────────────────────

export interface BreadcrumbItem {
  title: string;
  path?: string;
}

declare module "vue-router" {
  interface RouteMeta {
    requiresAuth?: boolean;
    requiresPermission?: string;
    fullscreen?: boolean;
    menuKey?: string;
    menuGroup?: string;
    breadcrumb?: BreadcrumbItem[];
  }
}

// ─── Routes ─────────────────────────────────────────────

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: "/login", name: "login", component: LoginPage },
    {
      path: "/",
      name: "home",
      component: HomePage,
      meta: { requiresAuth: true, menuKey: "home" }
    },
    {
      path: "/profile",
      name: "profile",
      component: ProfilePage,
      meta: { requiresAuth: true }
    },

    // ── 安全中心 ──
    {
      path: "/assets",
      name: "assets",
      component: AssetsPage,
      meta: {
        requiresAuth: true,
        menuKey: "assets",
        menuGroup: "security",
        breadcrumb: [{ title: "安全中心" }, { title: "资产管理" }]
      }
    },
    {
      path: "/alert",
      name: "alert",
      component: AlertPage,
      meta: {
        requiresAuth: true,
        menuKey: "alert",
        menuGroup: "security",
        breadcrumb: [{ title: "安全中心" }, { title: "告警管理" }]
      }
    },
    {
      path: "/audit",
      name: "audit",
      component: AuditPage,
      meta: {
        requiresAuth: true,
        menuKey: "audit",
        menuGroup: "security",
        breadcrumb: [{ title: "安全中心" }, { title: "审计日志" }]
      }
    },

    // ── 流程中心 ──
    {
      path: "/process/flows",
      name: "process-flows",
      component: ApprovalFlowsPage,
      meta: {
        requiresAuth: true,
        menuKey: "process-flows",
        menuGroup: "process",
        breadcrumb: [{ title: "流程中心" }, { title: "流程定义" }]
      }
    },
    {
      path: "/process/designer/:id?",
      name: "process-designer",
      component: ApprovalDesignerPage,
      meta: { requiresAuth: true, fullscreen: true, menuKey: "process-flows", menuGroup: "process" }
    },
    {
      path: "/process/tasks",
      name: "process-tasks",
      component: ApprovalTasksPage,
      meta: {
        requiresAuth: true,
        menuKey: "process-tasks",
        menuGroup: "process",
        breadcrumb: [{ title: "流程中心" }, { title: "我的待办" }]
      }
    },
    {
      path: "/process/instances",
      name: "process-instances",
      component: ApprovalInstancesPage,
      meta: {
        requiresAuth: true,
        menuKey: "process-instances",
        menuGroup: "process",
        breadcrumb: [{ title: "流程中心" }, { title: "我发起的" }]
      }
    },
    {
      path: "/process/monitor",
      name: "process-monitor",
      component: ProcessMonitorPage,
      meta: {
        requiresAuth: true,
        menuKey: "process-monitor",
        menuGroup: "process",
        breadcrumb: [{ title: "流程中心" }, { title: "流程监控" }]
      }
    },
    {
      path: "/process/workflow-designer",
      name: "workflow-designer",
      component: WorkflowDesignerPage,
      meta: {
        requiresAuth: true,
        requiresPermission: "workflow:design",
        menuKey: "process-flows",
        menuGroup: "process"
      }
    },
    {
      path: "/process/workflow-instances",
      name: "workflow-instances",
      component: WorkflowInstancesPage,
      meta: {
        requiresAuth: true,
        requiresPermission: "workflow:design",
        menuKey: "process-monitor",
        menuGroup: "process"
      }
    },

    // ── 应用中心 ──
    {
      path: "/apps/list",
      name: "app-list",
      component: AppListPage,
      meta: {
        requiresAuth: true,
        requiresPermission: "system:admin",
        menuKey: "apps-list",
        menuGroup: "apps",
        breadcrumb: [{ title: "应用中心" }, { title: "应用管理" }]
      }
    },
    {
      path: "/apps/builder/:id",
      name: "app-builder",
      component: AppBuilderPage,
      meta: { requiresAuth: true, requiresPermission: "system:admin", fullscreen: true, menuKey: "apps-list", menuGroup: "apps" }
    },
    {
      path: "/apps/forms",
      name: "apps-forms",
      component: FormListPage,
      meta: {
        requiresAuth: true,
        requiresPermission: "system:admin",
        menuKey: "apps-forms",
        menuGroup: "apps",
        breadcrumb: [{ title: "应用中心" }, { title: "表单管理" }]
      }
    },
    {
      path: "/apps/forms/designer/:id",
      name: "apps-form-designer",
      component: FormDesignerPage,
      meta: { requiresAuth: true, requiresPermission: "system:admin", fullscreen: true, menuKey: "apps-forms", menuGroup: "apps" }
    },
    {
      path: "/apps/data-model",
      name: "apps-data-model",
      component: DynamicTablesPage,
      meta: {
        requiresAuth: true,
        requiresPermission: "system:admin",
        menuKey: "apps-data-model",
        menuGroup: "apps",
        breadcrumb: [{ title: "应用中心" }, { title: "数据模型" }]
      }
    },
    {
      path: "/apps/data-model/:tableKey",
      name: "apps-data-model-crud",
      component: DynamicTableCrudPage,
      meta: { requiresAuth: true, requiresPermission: "system:admin", menuKey: "apps-data-model", menuGroup: "apps" }
    },

    // ── 系统设置 ──
    {
      path: "/settings/org/users",
      name: "settings-users",
      component: UsersPage,
      meta: {
        requiresAuth: true,
        requiresPermission: "users:view",
        menuKey: "settings-users",
        menuGroup: "settings",
        breadcrumb: [{ title: "系统设置" }, { title: "组织架构" }, { title: "员工管理" }]
      }
    },
    {
      path: "/settings/org/departments",
      name: "settings-departments",
      component: DepartmentsPage,
      meta: {
        requiresAuth: true,
        requiresPermission: "departments:view",
        menuKey: "settings-departments",
        menuGroup: "settings",
        breadcrumb: [{ title: "系统设置" }, { title: "组织架构" }, { title: "部门管理" }]
      }
    },
    {
      path: "/settings/org/positions",
      name: "settings-positions",
      component: PositionsPage,
      meta: {
        requiresAuth: true,
        requiresPermission: "positions:view",
        menuKey: "settings-positions",
        menuGroup: "settings",
        breadcrumb: [{ title: "系统设置" }, { title: "组织架构" }, { title: "职位管理" }]
      }
    },
    {
      path: "/settings/auth/roles",
      name: "settings-roles",
      component: RolesPage,
      meta: {
        requiresAuth: true,
        requiresPermission: "roles:view",
        menuKey: "settings-roles",
        menuGroup: "settings",
        breadcrumb: [{ title: "系统设置" }, { title: "角色权限" }, { title: "角色管理" }]
      }
    },
    {
      path: "/settings/auth/permissions",
      name: "settings-permissions",
      component: PermissionsPage,
      meta: {
        requiresAuth: true,
        requiresPermission: "permissions:view",
        menuKey: "settings-permissions",
        menuGroup: "settings",
        breadcrumb: [{ title: "系统设置" }, { title: "角色权限" }, { title: "权限管理" }]
      }
    },
    {
      path: "/settings/auth/menus",
      name: "settings-menus",
      component: MenusPage,
      meta: {
        requiresAuth: true,
        requiresPermission: "menus:view",
        menuKey: "settings-menus",
        menuGroup: "settings",
        breadcrumb: [{ title: "系统设置" }, { title: "角色权限" }, { title: "菜单管理" }]
      }
    },
    {
      path: "/settings/projects",
      name: "settings-projects",
      component: ProjectsPage,
      meta: {
        requiresAuth: true,
        requiresPermission: "projects:view",
        menuKey: "settings-projects",
        menuGroup: "settings",
        breadcrumb: [{ title: "系统设置" }, { title: "项目管理" }]
      }
    },
    {
      path: "/settings/apps",
      name: "settings-apps",
      component: AppsPage,
      meta: {
        requiresAuth: true,
        requiresPermission: "apps:view",
        menuKey: "settings-apps",
        menuGroup: "settings",
        breadcrumb: [{ title: "系统设置" }, { title: "应用配置" }]
      }
    },
    {
      path: "/settings/messages",
      name: "settings-messages",
      component: MessageCenterPage,
      meta: {
        requiresAuth: true,
        menuKey: "settings-messages",
        menuGroup: "settings",
        breadcrumb: [{ title: "系统设置" }, { title: "消息管理" }]
      }
    },

    // ── 全局功能 ──
    {
      path: "/ai",
      name: "ai-assistant",
      component: AiAssistantPage,
      meta: { requiresAuth: true }
    },
    {
      path: "/system/dict",
      name: "system-dict",
      component: DictTypesPage,
      meta: { requiresAuth: true, requiresPermission: "dict:type:view" }
    },
    {
      path: "/system/configs",
      name: "system-configs",
      component: SystemConfigsPage,
      meta: { requiresAuth: true, requiresPermission: "config:view" }
    },
    {
      path: "/system/login-logs",
      name: "system-login-logs",
      component: LoginLogsPage,
      meta: { requiresAuth: true, requiresPermission: "loginlog:view" }
    },
    {
      path: "/system/online-users",
      name: "system-online-users",
      component: OnlineUsersPage,
      meta: { requiresAuth: true, requiresPermission: "online:view" }
    },
    {
      path: "/system/notifications",
      name: "system-notifications",
      component: NotificationsPage,
      meta: { requiresAuth: true }
    },
    {
      path: "/monitor/server",
      name: "monitor-server",
      component: ServerInfoPage,
      meta: { requiresAuth: true, requiresPermission: "monitor:view" }
    },
    {
      path: "/monitor/jobs",
      name: "monitor-jobs",
      component: ScheduledJobsPage,
      meta: { requiresAuth: true, requiresPermission: "job:view" }
    },
    { path: "/visualization/center", name: "visualization-center", component: VisualizationCenterPage, meta: { requiresAuth: true } },
    { path: "/visualization/designer/:id?", name: "visualization-designer", component: VisualizationDesignerPage, meta: { requiresAuth: true } },
    { path: "/visualization/runtime", name: "visualization-runtime", component: VisualizationRuntimePage, meta: { requiresAuth: true } },
    { path: "/visualization/governance", name: "visualization-governance", component: VisualizationGovernancePage, meta: { requiresAuth: true } },

    // ── 旧路径重定向（防止书签失效）──
    { path: "/approval/flows", redirect: "/process/flows" },
    { path: "/approval/designer/:id?", redirect: (to) => `/process/designer/${to.params.id || ""}` },
    { path: "/approval/tasks", redirect: "/process/tasks" },
    { path: "/approval/instances", redirect: "/process/instances" },
    { path: "/system/users", redirect: "/settings/org/users" },
    { path: "/system/departments", redirect: "/settings/org/departments" },
    { path: "/system/positions", redirect: "/settings/org/positions" },
    { path: "/system/roles", redirect: "/settings/auth/roles" },
    { path: "/system/permissions", redirect: "/settings/auth/permissions" },
    { path: "/system/menus", redirect: "/settings/auth/menus" },
    { path: "/system/projects", redirect: "/settings/projects" },
    { path: "/system/apps", redirect: "/settings/apps" },
    { path: "/lowcode/forms", redirect: "/apps/forms" },
    { path: "/lowcode/apps", redirect: "/apps/list" },
    { path: "/lowcode/messages", redirect: "/settings/messages" },
    { path: "/lowcode/ai", redirect: "/ai" },
    { path: "/lowcode/process-monitor", redirect: "/process/monitor" },
    { path: "/dynamic-tables", redirect: "/apps/data-model" },
    { path: "/workflow/designer", redirect: "/process/workflow-designer" },
    { path: "/workflow/instances", redirect: "/process/workflow-instances" },

    // 404 catch-all route — must be last
    { path: "/:pathMatch(.*)*", name: "not-found", component: NotFoundPage }
  ]
});

router.beforeEach((to) => {
  const token = getAccessToken();
  const tenantId = getTenantId();
  const profile = getAuthProfile();

  // 要求登录：必须同时有 token + tenantId
  if (to.meta.requiresAuth && (!token || !tenantId)) {
    return { name: "login" };
  }

  if (to.meta.requiresPermission && typeof to.meta.requiresPermission === "string") {
    if (!hasPermission(profile, to.meta.requiresPermission)) {
      return { name: "home" };
    }
  }

  return true;
});

export default router;

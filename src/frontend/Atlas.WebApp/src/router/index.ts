import { createRouter, createWebHistory } from "vue-router";
import { getAccessToken, getAuthProfile, getTenantId, hasPermission } from "@/utils/auth";
const HomePage = () => import("@/pages/HomePage.vue");
const LoginPage = () => import("@/pages/LoginPage.vue");
const AssetsPage = () => import("@/pages/AssetsPage.vue");
const AuditPage = () => import("@/pages/AuditPage.vue");
const AlertPage = () => import("@/pages/AlertPage.vue");
const ApprovalFlowsPage = () => import("@/pages/ApprovalFlowsPage.vue");
const ApprovalDesignerPage = () => import("@/pages/ApprovalDesignerPage.vue");
const ApprovalTasksPage = () => import("@/pages/ApprovalTasksPage.vue");
const ApprovalInstancesPage = () => import("@/pages/ApprovalInstancesPage.vue");
const WorkflowDesignerPage = () => import("@/pages/WorkflowDesignerPage.vue");
const WorkflowInstancesPage = () => import("@/pages/WorkflowInstancesPage.vue");
const VisualizationCenterPage = () => import("@/pages/visualization/VisualizationCenterPage.vue");
const VisualizationDesignerPage = () => import("@/pages/visualization/VisualizationDesignerPage.vue");
const VisualizationRuntimePage = () => import("@/pages/visualization/VisualizationRuntimePage.vue");
const VisualizationGovernancePage = () => import("@/pages/visualization/VisualizationGovernancePage.vue");
const ProfilePage = () => import("@/pages/ProfilePage.vue");
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

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: "/login", name: "login", component: LoginPage },
    { path: "/", name: "home", component: HomePage, meta: { requiresAuth: true } },
    { path: "/profile", name: "profile", component: ProfilePage, meta: { requiresAuth: true } },
    { path: "/assets", name: "assets", component: AssetsPage, meta: { requiresAuth: true } },
    { path: "/audit", name: "audit", component: AuditPage, meta: { requiresAuth: true } },
    { path: "/alert", name: "alert", component: AlertPage, meta: { requiresAuth: true } },
    { path: "/approval/flows", name: "approval-flows", component: ApprovalFlowsPage, meta: { requiresAuth: true } },
    { path: "/approval/designer/:id?", name: "approval-designer", component: ApprovalDesignerPage, meta: { requiresAuth: true, fullscreen: true } },
    { path: "/approval/tasks", name: "approval-tasks", component: ApprovalTasksPage, meta: { requiresAuth: true } },
    { path: "/approval/instances", name: "approval-instances", component: ApprovalInstancesPage, meta: { requiresAuth: true } },
    {
      path: "/system/users",
      name: "system-users",
      component: UsersPage,
      meta: { requiresAuth: true, requiresPermission: "users:view" }
    },
    {
      path: "/amis/system/users",
      name: "amis-system-users",
      component: AmisSystemPage,
      meta: { requiresAuth: true, requiresPermission: "users:view", amisKey: "system.users" }
    },
    {
      path: "/system/roles",
      name: "system-roles",
      component: RolesPage,
      meta: { requiresAuth: true, requiresPermission: "roles:view" }
    },
    {
      path: "/amis/system/roles",
      name: "amis-system-roles",
      component: AmisSystemPage,
      meta: { requiresAuth: true, requiresPermission: "roles:view", amisKey: "system.roles" }
    },
    {
      path: "/system/permissions",
      name: "system-permissions",
      component: PermissionsPage,
      meta: { requiresAuth: true, requiresPermission: "permissions:view" }
    },
    {
      path: "/amis/system/permissions",
      name: "amis-system-permissions",
      component: AmisSystemPage,
      meta: { requiresAuth: true, requiresPermission: "permissions:view", amisKey: "system.permissions" }
    },
    {
      path: "/system/menus",
      name: "system-menus",
      component: MenusPage,
      meta: { requiresAuth: true, requiresPermission: "menus:view" }
    },
    {
      path: "/amis/system/menus",
      name: "amis-system-menus",
      component: AmisSystemPage,
      meta: { requiresAuth: true, requiresPermission: "menus:view", amisKey: "system.menus" }
    },
    {
      path: "/system/departments",
      name: "system-departments",
      component: DepartmentsPage,
      meta: { requiresAuth: true, requiresPermission: "departments:view" }
    },
    {
      path: "/amis/system/departments",
      name: "amis-system-departments",
      component: AmisSystemPage,
      meta: { requiresAuth: true, requiresPermission: "departments:view", amisKey: "system.departments" }
    },
    {
      path: "/system/positions",
      name: "system-positions",
      component: PositionsPage,
      meta: { requiresAuth: true, requiresPermission: "positions:view" }
    },
    {
      path: "/amis/system/positions",
      name: "amis-system-positions",
      component: AmisSystemPage,
      meta: { requiresAuth: true, requiresPermission: "positions:view", amisKey: "system.positions" }
    },
    {
      path: "/system/apps",
      name: "system-apps",
      component: AppsPage,
      meta: { requiresAuth: true, requiresPermission: "apps:view" }
    },
    {
      path: "/amis/system/apps",
      name: "amis-system-apps",
      component: AmisSystemPage,
      meta: { requiresAuth: true, requiresPermission: "apps:view", amisKey: "system.apps" }
    },
    {
      path: "/system/projects",
      name: "system-projects",
      component: ProjectsPage,
      meta: { requiresAuth: true, requiresPermission: "projects:view" }
    },
    {
      path: "/amis/system/projects",
      name: "amis-system-projects",
      component: AmisSystemPage,
      meta: { requiresAuth: true, requiresPermission: "projects:view", amisKey: "system.projects" }
    },
    {
      path: "/workflow/designer",
      name: "workflow-designer",
      component: WorkflowDesignerPage,
      meta: { requiresAuth: true, requiresPermission: "workflow:design" }
    },
    {
      path: "/workflow/instances",
      name: "workflow-instances",
      component: WorkflowInstancesPage,
      meta: { requiresAuth: true, requiresPermission: "workflow:design" }
    },
    {
      path: "/dynamic-tables",
      name: "dynamic-tables",
      component: DynamicTablesPage,
      meta: { requiresAuth: true, requiresPermission: "system:admin" }
    },
    {
      path: "/dynamic-tables/:tableKey",
      name: "dynamic-table-crud",
      component: DynamicTableCrudPage,
      meta: { requiresAuth: true, requiresPermission: "system:admin" }
    },
    { path: "/visualization/center", name: "visualization-center", component: VisualizationCenterPage, meta: { requiresAuth: true } },
    { path: "/visualization/designer/:id?", name: "visualization-designer", component: VisualizationDesignerPage, meta: { requiresAuth: true } },
    { path: "/visualization/runtime", name: "visualization-runtime", component: VisualizationRuntimePage, meta: { requiresAuth: true } },
    { path: "/visualization/governance", name: "visualization-governance", component: VisualizationGovernancePage, meta: { requiresAuth: true } },
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

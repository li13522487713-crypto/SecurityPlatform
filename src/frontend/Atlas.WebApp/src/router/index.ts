import { createRouter, createWebHistory } from "vue-router";
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

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: "/login", name: "login", component: LoginPage },
    { path: "/", name: "home", component: HomePage, meta: { requiresAuth: true } },
    { path: "/assets", name: "assets", component: AssetsPage, meta: { requiresAuth: true } },
    { path: "/audit", name: "audit", component: AuditPage, meta: { requiresAuth: true } },
    { path: "/alert", name: "alert", component: AlertPage, meta: { requiresAuth: true } },
    { path: "/approval/flows", name: "approval-flows", component: ApprovalFlowsPage, meta: { requiresAuth: true } },
    { path: "/approval/designer/:id?", name: "approval-designer", component: ApprovalDesignerPage, meta: { requiresAuth: true } },
    { path: "/approval/tasks", name: "approval-tasks", component: ApprovalTasksPage, meta: { requiresAuth: true } },
    { path: "/approval/instances", name: "approval-instances", component: ApprovalInstancesPage, meta: { requiresAuth: true } },
    { path: "/workflow/designer", name: "workflow-designer", component: WorkflowDesignerPage, meta: { requiresAuth: false, requiresTenant: true } },
    { path: "/workflow/instances", name: "workflow-instances", component: WorkflowInstancesPage, meta: { requiresAuth: false, requiresTenant: true } },
    { path: "/visualization/center", name: "visualization-center", component: VisualizationCenterPage, meta: { requiresAuth: true } },
    { path: "/visualization/designer/:id?", name: "visualization-designer", component: VisualizationDesignerPage, meta: { requiresAuth: true } },
    { path: "/visualization/runtime", name: "visualization-runtime", component: VisualizationRuntimePage, meta: { requiresAuth: true } },
    { path: "/visualization/governance", name: "visualization-governance", component: VisualizationGovernancePage, meta: { requiresAuth: true } }
  ]
});

router.beforeEach((to) => {
  const token = localStorage.getItem("access_token");
  const tenantId = localStorage.getItem("tenant_id");

  // 只要求租户：没有 tenantId 也不允许进入（否则 API 会返回“无效或缺失租户标识”）
  if (to.meta.requiresTenant && !tenantId) {
    return { name: "login" };
  }

  // 要求登录：必须同时有 token + tenantId
  if (to.meta.requiresAuth && (!token || !tenantId)) {
    return { name: "login" };
  }

  return true;
});

export default router;

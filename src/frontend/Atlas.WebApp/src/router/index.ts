import { createRouter, createWebHistory } from "vue-router";
import HomePage from "@/pages/HomePage.vue";
import LoginPage from "@/pages/LoginPage.vue";
import AssetsPage from "@/pages/AssetsPage.vue";
import AuditPage from "@/pages/AuditPage.vue";
import AlertPage from "@/pages/AlertPage.vue";
import ApprovalFlowsPage from "@/pages/ApprovalFlowsPage.vue";
import ApprovalDesignerPage from "@/pages/ApprovalDesignerPage.vue";
import ApprovalTasksPage from "@/pages/ApprovalTasksPage.vue";
import ApprovalInstancesPage from "@/pages/ApprovalInstancesPage.vue";
import WorkflowDesignerPage from "@/pages/WorkflowDesignerPage.vue";
import WorkflowInstancesPage from "@/pages/WorkflowInstancesPage.vue";

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
    { path: "/workflow/instances", name: "workflow-instances", component: WorkflowInstancesPage, meta: { requiresAuth: false, requiresTenant: true } }
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
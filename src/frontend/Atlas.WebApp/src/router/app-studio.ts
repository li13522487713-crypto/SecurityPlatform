import { createRouter, createWebHistory } from "vue-router";
import { getAccessToken, getTenantId } from "@/utils/auth";

const NotFoundPage = () => import("@/pages/NotFoundPage.vue");
const AppWorkspaceLayout = () => import("@/layouts/AppWorkspaceLayout.vue");
const AppDashboardPage = () => import("@/pages/apps/AppDashboardPage.vue");
const AppBuilderPage = () => import("@/pages/lowcode/AppBuilderPage.vue");
const AppPagesPage = () => import("@/pages/apps/AppPagesPage.vue");
const FormListPage = () => import("@/pages/lowcode/FormListPage.vue");
const ApprovalFlowsPage = () => import("@/pages/ApprovalFlowsPage.vue");
const AppSettingsPage = () => import("@/pages/apps/AppSettingsPage.vue");

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: "/",
      component: AppWorkspaceLayout,
      meta: { requiresAuth: true },
      children: [
        { path: "apps/:appId", redirect: (to) => `/apps/${String(to.params.appId)}/dashboard` },
        { path: "apps/:appId/dashboard", name: "studio-dashboard", component: AppDashboardPage, meta: { requiresAuth: true } },
        { path: "apps/:appId/builder", name: "studio-builder", component: AppBuilderPage, meta: { requiresAuth: true } },
        { path: "apps/:appId/pages", name: "studio-pages", component: AppPagesPage, meta: { requiresAuth: true } },
        { path: "apps/:appId/forms", name: "studio-forms", component: FormListPage, meta: { requiresAuth: true } },
        { path: "apps/:appId/flows", name: "studio-flows", component: ApprovalFlowsPage, meta: { requiresAuth: true } },
        { path: "apps/:appId/settings", name: "studio-settings", component: AppSettingsPage, meta: { requiresAuth: true } }
      ]
    },
    { path: "/:pathMatch(.*)*", name: "studio-not-found", component: NotFoundPage }
  ]
});

router.beforeEach((to) => {
  const authenticated = Boolean(getAccessToken() && getTenantId());
  if (to.meta.requiresAuth && !authenticated) {
    return `/login?redirect=${encodeURIComponent(to.fullPath)}`;
  }

  return true;
});

export default router;

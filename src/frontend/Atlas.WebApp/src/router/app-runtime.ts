import { createRouter, createWebHistory } from "vue-router";
import { getAccessToken, getTenantId } from "@/utils/auth";

const AppHostLayout = () => import("@/layouts/AppHostLayout.vue");
const AppEntryGatewayPage = () => import("@/pages/app-entry/AppEntryGatewayPage.vue");
const AppPageRuntimeRenderer = () => import("@/pages/app-runtime/AppPageRuntimeRenderer.vue");

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: "/app-host/:appKey",
      component: AppHostLayout,
      meta: { requiresAuth: true },
      children: [
        { path: "", name: "app-runtime-entry", component: AppEntryGatewayPage, meta: { requiresAuth: true } },
        { path: "r/:pageKey", name: "app-runtime-page", component: AppPageRuntimeRenderer, meta: { requiresAuth: true } }
      ]
    },
    {
      path: "/r/:appKey/:pageKey",
      redirect: (to) => `/app-host/${encodeURIComponent(String(to.params.appKey))}/r/${encodeURIComponent(String(to.params.pageKey))}`
    }
  ]
});

router.beforeEach((to) => {
  const authenticated = Boolean(getAccessToken() && getTenantId());
  if (to.meta.requiresAuth && !authenticated) {
    const appKey = typeof to.params.appKey === "string" ? to.params.appKey : "";
    return `/app-host/${encodeURIComponent(appKey)}/login?redirect=${encodeURIComponent(to.fullPath)}`;
  }

  return true;
});

export default router;

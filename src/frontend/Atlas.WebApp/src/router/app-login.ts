import { createRouter, createWebHistory } from "vue-router";
import { getAccessToken, getTenantId } from "@/utils/auth";

const AppLoginPage = () => import("@/pages/app-login/AppLoginPage.vue");
const AppEntryGatewayPage = () => import("@/pages/app-entry/AppEntryGatewayPage.vue");

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: "/app-host/:appKey/login", name: "app-login", component: AppLoginPage },
    { path: "/app-host/:appKey/entry", name: "app-login-entry", component: AppEntryGatewayPage, meta: { requiresAuth: true } }
  ]
});

router.beforeEach((to) => {
  const appKey = typeof to.params.appKey === "string" ? to.params.appKey : "";
  const authenticated = Boolean(getAccessToken() && getTenantId());
  if (to.name === "app-login" && authenticated) {
    return `/app-host/${encodeURIComponent(appKey)}/entry`;
  }

  if (to.meta.requiresAuth && !authenticated) {
    return `/app-host/${encodeURIComponent(appKey)}/login?redirect=${encodeURIComponent(to.fullPath)}`;
  }

  return true;
});

export default router;

import { createRouter, createWebHistory } from "vue-router";
import { getAccessToken, getTenantId } from "@/utils/auth";

const LoginPage = () => import("@/pages/LoginPage.vue");
const RegisterPage = () => import("@/pages/RegisterPage.vue");
const ProfilePage = () => import("@/pages/ProfilePage.vue");
const NotFoundPage = () => import("@/pages/NotFoundPage.vue");
const ConsoleLayout = () => import("@/layouts/ConsoleLayout.vue");
const ConsolePage = () => import("@/pages/console/ConsolePage.vue");
const ApplicationCatalogPage = () => import("@/pages/console/ApplicationCatalogPage.vue");
const RuntimeContextsPage = () => import("@/pages/console/RuntimeContextsPage.vue");
const RuntimeExecutionsPage = () => import("@/pages/console/RuntimeExecutionsPage.vue");
const ReleaseCenterPage = () => import("@/pages/console/ReleaseCenterPage.vue");
const AppDatabaseMigrationPage = () => import("@/pages/console/AppDatabaseMigrationPage.vue");
const NotificationsPage = () => import("@/pages/system/NotificationsPage.vue");
const TenantDataSourcesPage = () => import("@/pages/system/TenantDataSourcesPage.vue");
const SystemConfigsPage = () => import("@/pages/system/SystemConfigsPage.vue");
const ServerInfoPage = () => import("@/pages/monitor/ServerInfoPage.vue");

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: "/", redirect: "/console" },
    { path: "/login", name: "platform-login", component: LoginPage },
    { path: "/register", name: "platform-register", component: RegisterPage },
    {
      path: "/",
      component: ConsoleLayout,
      meta: { requiresAuth: true },
      children: [
        { path: "console", name: "platform-console-home", component: ConsolePage, meta: { requiresAuth: true } },
        { path: "console/catalog", name: "platform-console-catalog", component: ApplicationCatalogPage, meta: { requiresAuth: true } },
        { path: "console/runtime-contexts", name: "platform-console-runtime-contexts", component: RuntimeContextsPage, meta: { requiresAuth: true } },
        { path: "console/runtime-executions", name: "platform-console-runtime-executions", component: RuntimeExecutionsPage, meta: { requiresAuth: true } },
        { path: "console/releases", name: "platform-console-releases", component: ReleaseCenterPage, meta: { requiresAuth: true } },
        { path: "console/app-db-migrations", name: "platform-console-app-db-migrations", component: AppDatabaseMigrationPage, meta: { requiresAuth: true } },
        { path: "profile", name: "platform-profile", component: ProfilePage, meta: { requiresAuth: true } },
        { path: "system/notifications", name: "platform-notifications", component: NotificationsPage, meta: { requiresAuth: true } },
        { path: "settings/system/datasources", name: "platform-datasources", component: TenantDataSourcesPage, meta: { requiresAuth: true } },
        { path: "settings/system/configs", name: "platform-system-configs", component: SystemConfigsPage, meta: { requiresAuth: true } },
        { path: "monitor/server-info", name: "platform-server-info", component: ServerInfoPage, meta: { requiresAuth: true } }
      ]
    },
    { path: "/:pathMatch(.*)*", name: "platform-not-found", component: NotFoundPage }
  ]
});

router.beforeEach((to) => {
  const authenticated = Boolean(getAccessToken() && getTenantId());
  if (to.path === "/login" && authenticated) {
    return "/console";
  }

  if (to.meta.requiresAuth && !authenticated) {
    return `/login?redirect=${encodeURIComponent(to.fullPath)}`;
  }

  return true;
});

export default router;

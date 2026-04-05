import { createRouter, createWebHistory } from "vue-router";
import { getAccessToken } from "@atlas/shared-core";
import { useAppUserStore } from "@/stores/user";
import { APP_PERMISSIONS } from "@/constants/permissions";

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: "/",
      name: "home",
      component: () => import("@/pages/HomePage.vue"),
      meta: { requiresAuth: false }
    },
    {
      path: "/apps/:appKey/login",
      name: "app-login",
      component: () => import("@/pages/app-login/AppLoginPage.vue"),
      meta: { requiresAuth: false }
    },
    {
      path: "/apps/:appKey",
      component: () => import("@/layouts/AppWorkspaceLayout.vue"),
      meta: { requiresAuth: true },
      children: [
        {
          path: "",
          redirect: (to) => `/apps/${String(to.params.appKey)}/dashboard`
        },
        {
          path: "entry",
          name: "app-entry",
          component: () => import("@/pages/app-entry/AppEntryGatewayPage.vue")
        },
        {
          path: "dashboard",
          name: "app-dashboard",
          component: () => import("@/pages/dashboard/AppDashboardPage.vue")
        },
        {
          path: "r/:pageKey",
          name: "app-runtime-page",
          component: () => import("@/pages/runtime/PageRuntimeRenderer.vue")
        },
        {
          path: "org",
          name: "app-org",
          component: () => import("@/pages/org/AppOrganizationPage.vue"),
          meta: { requiredPermission: APP_PERMISSIONS.APP_MEMBERS_VIEW }
        },
        {
          path: "ai/chat/:agentId?",
          name: "app-ai-chat",
          component: () => import("@/pages/ai/AgentChatPage.vue")
        },
        {
          path: "ai/assistant",
          name: "app-ai-assistant",
          component: () => import("@/pages/ai/AiAssistantPage.vue")
        },
        {
          path: "approval",
          name: "app-approval",
          component: () => import("@/pages/approval/ApprovalWorkspacePage.vue")
        },
        {
          path: "approval/:instanceId",
          name: "app-approval-detail",
          component: () => import("@/pages/approval/ApprovalInstanceDetailPage.vue")
        },
        {
          path: "reports",
          name: "app-reports",
          component: () => import("@/pages/reports/ReportsPage.vue")
        },
        {
          path: "dashboards",
          name: "app-dashboards",
          component: () => import("@/pages/reports/DashboardsPage.vue")
        },
        {
          path: "visualization",
          name: "app-visualization",
          component: () => import("@/pages/visualization/VisualizationRuntimePage.vue")
        },
        {
          path: "settings",
          name: "app-settings",
          component: () => import("@/pages/settings/AppSettingsPage.vue"),
          meta: { requiredPermission: APP_PERMISSIONS.APPS_UPDATE }
        },
        {
          path: "forbidden",
          name: "app-forbidden",
          component: () => import("@/pages/ForbiddenPage.vue")
        }
      ]
    },
    {
      path: "/:pathMatch(.*)*",
      name: "not-found",
      redirect: "/"
    }
  ]
});

function checkPermission(permission: string | undefined, userStore: ReturnType<typeof useAppUserStore>): boolean {
  if (!permission) return true;
  if (userStore.profile?.isPlatformAdmin) return true;
  if (userStore.permissions.includes("*:*:*")) return true;
  if (userStore.roles.some((r) => ["admin", "superadmin"].includes(r.toLowerCase()))) return true;
  return userStore.permissions.includes(permission);
}

router.beforeEach(async (to, _from, next) => {
  if (to.meta.requiresAuth !== true) {
    next();
    return;
  }

  const token = getAccessToken();
  if (!token) {
    const appKey = typeof to.params.appKey === "string" ? to.params.appKey : "";
    if (appKey) {
      next({
        name: "app-login",
        params: { appKey },
        query: { redirect: to.fullPath }
      });
    } else {
      next({ name: "home" });
    }
    return;
  }

  const userStore = useAppUserStore();
  if (!userStore.profile) {
    try {
      await userStore.getInfo();
    } catch {
      userStore.hydrateFromStorage();
    }
  }

  const requiredPermission = to.meta.requiredPermission as string | undefined;
  if (requiredPermission && !checkPermission(requiredPermission, userStore)) {
    const appKey = typeof to.params.appKey === "string" ? to.params.appKey : "";
    next({ name: "app-forbidden", params: { appKey } });
    return;
  }

  next();
});

import { createRouter, createWebHistory } from "vue-router";
import { getAccessToken } from "@atlas/shared-core";

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
      path: "/apps/:appKey/entry",
      name: "app-entry",
      component: () => import("@/pages/app-entry/AppEntryGatewayPage.vue"),
      meta: { requiresAuth: true }
    },
    {
      path: "/apps/:appKey",
      component: () => import("@/layouts/AppRuntimeLayout.vue"),
      meta: { requiresAuth: true },
      children: [
        {
          path: "r/:pageKey",
          name: "app-runtime-page",
          component: () => import("@/pages/runtime/PageRuntimeRenderer.vue")
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

router.beforeEach((to, _from, next) => {
  if (to.meta.requiresAuth !== true) {
    next();
    return;
  }

  const token = getAccessToken();
  if (token) {
    next();
  } else {
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
  }
});

import { createRouter, createWebHistory } from "vue-router";
import { getAccessToken, getRefreshToken } from "@atlas/shared-core";
import { useAppUserStore } from "@/stores/user";
import { APP_PERMISSIONS } from "@/constants/permissions";
import { getSetupState } from "@/services/api-setup";
import { refreshToken as refreshAccessToken } from "@/services/api-auth";

let setupChecked = false;
let platformReady = true;
let appReady = true;
let configuredAppKey = "";
const LAST_APP_KEY_STORAGE = "atlas_app_last_appkey";
let refreshTokenInflight: Promise<boolean> | null = null;

export function markAppSetupComplete() {
  appReady = true;
  setupChecked = true;
}

export function getConfiguredAppKey(): string {
  if (!configuredAppKey && typeof window !== "undefined") {
    configuredAppKey = localStorage.getItem(LAST_APP_KEY_STORAGE) ?? "";
  }
  return configuredAppKey;
}

function rememberConfiguredAppKey(appKey?: string | null) {
  const normalized = String(appKey ?? "").trim();
  if (!normalized) {
    return;
  }

  configuredAppKey = normalized;
  if (typeof window !== "undefined") {
    localStorage.setItem(LAST_APP_KEY_STORAGE, normalized);
  }
}

function normalizeAppRoutePath(path: string, appKey: string): string {
  return path.replace(/^\/apps\/[^/]+/, `/apps/${encodeURIComponent(appKey)}`);
}

async function ensureAccessToken(): Promise<string | null> {
  const token = getAccessToken();
  if (token) {
    return token;
  }

  if (!getRefreshToken()) {
    return null;
  }

  if (!refreshTokenInflight) {
    refreshTokenInflight = refreshAccessToken().finally(() => {
      refreshTokenInflight = null;
    });
  }

  const refreshed = await refreshTokenInflight;
  return refreshed ? getAccessToken() : null;
}

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: "/platform-not-ready",
      name: "platform-not-ready",
      component: () => import("@/pages/PlatformNotReadyPage.vue"),
      meta: { requiresAuth: false }
    },
    {
      path: "/app-setup",
      name: "app-setup",
      component: () => import("@/pages/AppSetupWizardPage.vue"),
      meta: { requiresAuth: false }
    },
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
          component: () => import("@/runtime/hosts/RuntimePageHost.vue")
        },
        {
          path: "org",
          redirect: (to) => `/apps/${String(to.params.appKey)}/users`
        },
        {
          path: "users",
          name: "app-users",
          component: () => import("@/pages/system/AppUsersPage.vue"),
          meta: { requiredPermission: APP_PERMISSIONS.APP_ROLES_VIEW }
        },
        {
          path: "departments",
          name: "app-departments",
          component: () => import("@/pages/system/AppDepartmentsPage.vue"),
          meta: { requiredPermission: APP_PERMISSIONS.APP_ROLES_VIEW }
        },
        {
          path: "positions",
          name: "app-positions",
          component: () => import("@/pages/system/AppPositionsPage.vue"),
          meta: { requiredPermission: APP_PERMISSIONS.APP_MEMBERS_VIEW }
        },
        {
          path: "capabilities/organization",
          name: "app-capability-organization",
          component: () => import("@atlas/capability-ui").then((m) => m.OrganizationCapabilityPage)
        },
        {
          path: "ai/agents",
          name: "app-ai-agents",
          component: () => import("@/pages/ai/AgentManagePage.vue")
        },
        {
          path: "capabilities/agent",
          name: "app-capability-agent",
          component: () => import("@atlas/capability-ui").then((m) => m.AgentCapabilityPage)
        },
        {
          path: "capabilities/workflow",
          name: "app-capability-workflow",
          component: () => import("@atlas/capability-ui").then((m) => m.WorkflowCapabilityPage)
        },
        {
          path: "capabilities/knowledge",
          name: "app-capability-knowledge",
          component: () => import("@atlas/capability-ui").then((m) => m.KnowledgeCapabilityPage)
        },
        {
          path: "capabilities/data",
          name: "app-capability-data",
          component: () => import("@atlas/capability-ui").then((m) => m.DataCapabilityPage)
        },
        {
          path: "capabilities/connector",
          name: "app-capability-connector",
          component: () => import("@atlas/capability-ui").then((m) => m.ConnectorCapabilityPage)
        },
        {
          path: "capabilities/runtime",
          name: "app-capability-runtime",
          component: () => import("@atlas/capability-ui").then((m) => m.RuntimeCapabilityPage)
        },
        {
          path: "capabilities/release",
          name: "app-capability-release",
          component: () => import("@atlas/capability-ui").then((m) => m.ReleaseCapabilityPage)
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
          path: "builder",
          name: "app-builder",
          component: () => import("@/pages/PlaceholderPage.vue"),
          props: { title: "应用设计器" }
        },
        {
          path: "roles",
          name: "app-roles",
          component: () => import("@/pages/system/AppRolesPage.vue"),
          meta: { requiredPermission: APP_PERMISSIONS.APP_ROLES_VIEW }
        },
        {
          path: "multi-agent",
          name: "app-multi-agent",
          component: () => import("@/pages/PlaceholderPage.vue"),
          props: { title: "多 Agent 编排" }
        },
        {
          path: "workflows",
          name: "app-workflows",
          component: () => import("@/pages/workflow/WorkflowListPage.vue")
        },
        {
          path: "workflows/:id/editor",
          name: "app-workflow-editor",
          component: () => import("@/pages/workflow/WorkflowEditorPage.vue")
        },
        {
          path: "workflow-databases",
          name: "app-workflow-databases",
          component: () => import("@/pages/workflow/WorkflowDatabasesPage.vue")
        },
        {
          path: "logic-flow",
          name: "app-logic-flow",
          component: () => import("@/pages/PlaceholderPage.vue"),
          props: { title: "逻辑与批处理" }
        },
        {
          path: "knowledge-bases",
          name: "app-knowledge-bases",
          component: () => import("@/pages/PlaceholderPage.vue"),
          props: { title: "知识库配置" }
        },
        {
          path: "prompts",
          name: "app-prompts",
          component: () => import("@/pages/PlaceholderPage.vue"),
          props: { title: "Prompt 资源" }
        },
        {
          path: "model-configs",
          name: "app-model-configs",
          component: () => import("@/pages/ai/AppModelConfigsPage.vue")
        },
        {
          path: "evaluations",
          name: "app-evaluations",
          component: () => import("@/pages/PlaceholderPage.vue"),
          props: { title: "模型与评测" }
        },
        {
          path: "data",
          name: "app-data",
          component: () => import("@/pages/dynamic/DynamicTablesPage.vue")
        },
        {
          path: "data/:tableKey",
          name: "app-data-records",
          component: () => import("@/pages/dynamic/DynamicTableRecordsPage.vue")
        },
        {
          path: "data/:tableKey/design",
          name: "app-data-design",
          component: () => import("@/pages/dynamic/DynamicTableDesignPage.vue")
        },
        {
          path: "profile",
          name: "app-profile",
          component: () => import("@/pages/profile/ProfilePage.vue")
        },
        {
          path: "settings",
          name: "app-settings",
          component: () => import("@/pages/settings/AppSettingsPage.vue"),
          meta: { requiredPermission: APP_PERMISSIONS.APPS_UPDATE }
        },
        {
          path: "connectors/config",
          name: "app-connectors-config",
          component: () => import("@/pages/settings/ConnectorConfigPage.vue")
        },
        {
          path: "connectors/exposure",
          name: "app-connectors-exposure",
          component: () => import("@/pages/settings/ConnectorExposurePage.vue")
        },
        {
          path: "connectors/authorization",
          name: "app-connectors-authorization",
          component: () => import("@/pages/settings/ConnectorAuthorizationPage.vue")
        },
        {
          path: "connectors/command-strategy",
          name: "app-connectors-command-strategy",
          component: () => import("@/pages/settings/ConnectorCommandStrategyPage.vue")
        },
        {
          path: "forbidden",
          name: "app-forbidden",
          component: () => import("@/pages/ForbiddenPage.vue")
        }
      ]
    },
    {
      path: "/r/:appKey/:pageKey",
      name: "public-runtime-page",
      component: () => import("@/runtime/hosts/RuntimePageHost.vue"),
      meta: { requiresAuth: true }
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
  if (!setupChecked) {
    try {
      const resp = await getSetupState();
      const platformStatus = String(resp.data?.platformStatus ?? "").trim();
      const platformSetupCompleted = resp.data?.platformSetupCompleted === true;
      platformReady = resp.success && (platformSetupCompleted || platformStatus === "Ready");
      appReady = platformReady && resp.data?.appSetupCompleted === true;
      if (resp.success) rememberConfiguredAppKey(resp.data?.appKey);
    } catch {
      platformReady = false;
      appReady = false;
    }
    setupChecked = true;
  }

  if (!platformReady && to.name !== "platform-not-ready" && to.name !== "app-setup") {
    next({ name: "platform-not-ready" });
    return;
  }

  if (platformReady && to.name === "platform-not-ready") {
    if (!appReady) {
      next({ name: "app-setup" });
    } else {
      next({ name: "home" });
    }
    return;
  }

  if (platformReady && !appReady && to.name !== "app-setup") {
    next({ name: "app-setup" });
    return;
  }

  const routeAppKey = typeof to.params.appKey === "string" ? to.params.appKey.trim() : "";
  const canonicalAppKey = getConfiguredAppKey().trim();
  if (routeAppKey && canonicalAppKey && routeAppKey !== canonicalAppKey) {
    next({
      path: normalizeAppRoutePath(to.fullPath, canonicalAppKey),
      replace: true
    });
    return;
  }

  if (appReady && to.name === "app-setup") {
    next({ name: "home" });
    return;
  }

  if (to.meta.requiresAuth !== true) {
    next();
    return;
  }

  const token = await ensureAccessToken();
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

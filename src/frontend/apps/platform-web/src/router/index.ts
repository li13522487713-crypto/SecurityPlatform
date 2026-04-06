import { createRouter, createWebHistory } from "vue-router";
import { getAccessToken } from "@atlas/shared-core";
import { applyPermissionMetaToRoutePath } from "./route-access";
import { getSetupState } from "@/services/api-setup";

const Placeholder = () => import("@/components/common/FeaturePlaceholder.vue");

let setupChecked = false;
let setupReady = true;

export function markSetupComplete() {
  setupReady = true;
  setupChecked = true;
}

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: "/setup",
      name: "setup",
      component: () => import("@/pages/SetupWizardPage.vue"),
      meta: { requiresAuth: false }
    },
    {
      path: "/login",
      name: "login",
      component: () => import("@/pages/LoginPage.vue"),
      meta: { requiresAuth: false }
    },
    { path: "/register", redirect: "/login" },
    { path: "/", redirect: "/console" },
    {
      path: "/",
      component: () => import("@/layouts/ConsoleLayout.vue"),
      meta: { requiresAuth: true },
      children: [
        // ── 控制台 ──
        { path: "console", name: "console-home", component: () => import("@/pages/HomePage.vue") },
        { path: "profile", name: "profile", component: () => import("@/pages/ProfilePage.vue") },
        { path: "console/catalog", name: "console-catalog", component: () => import("@/pages/console/ApplicationCatalogPage.vue") },
        { path: "console/tenant-applications", name: "console-tenant-apps", component: () => import("@/pages/console/TenantApplicationsPage.vue") },
        { path: "console/runtime-contexts", name: "console-runtime-contexts", component: () => import("@/pages/console/RuntimeContextsPage.vue") },
        { path: "console/runtime-executions", name: "console-runtime-executions", component: () => import("@/pages/console/RuntimeExecutionsPage.vue") },
        { path: "console/resources", name: "console-resources", component: () => import("@/pages/console/ResourceCenterPage.vue") },
        { path: "console/releases", name: "console-releases", component: () => import("@/pages/console/ReleaseCenterPage.vue") },
        { path: "console/migration-governance", name: "console-migration-governance", component: () => import("@/pages/console/MigrationGovernancePage.vue") },

        // ── 系统组织管理 ──
        { path: "settings/org/tenants", name: "sys-tenants", component: () => import("@/pages/system/TenantsPage.vue") },
        { path: "settings/org/users", name: "sys-users", component: () => import("@/pages/system/UsersPage.vue") },
        { path: "settings/org/departments", name: "sys-departments", component: () => import("@/pages/system/DepartmentsPage.vue") },
        { path: "settings/org/positions", name: "sys-positions", component: () => import("@/pages/system/PositionsPage.vue") },
        { path: "settings/auth/roles", name: "sys-roles", component: () => import("@/pages/system/RolesPage.vue") },
        { path: "settings/auth/menus", name: "sys-menus", component: () => import("@/pages/system/MenusPage.vue") },
        { path: "system/notifications", name: "sys-notifications", component: () => import("@/pages/system/NotificationsPage.vue") },
        { path: "system/login-logs", name: "sys-login-logs", component: () => import("@/pages/system/LoginLogsPage.vue") },
        { path: "system/online-users", name: "sys-online-users", component: () => import("@/pages/system/OnlineUsersPage.vue") },

        // ── 设置与集成 ──
        { path: "settings/system/dict-types", name: "settings-dict", component: () => import("@/pages/settings/DictTypesPage.vue") },
        { path: "settings/system/datasources", name: "settings-datasources", component: () => import("@/pages/settings/DataSourcesPage.vue") },
        { path: "settings/system/configs", name: "settings-configs", component: () => import("@/pages/settings/SystemConfigsPage.vue") },
        { path: "settings/system/database", name: "settings-database", component: () => import("@/pages/settings/DatabaseMaintenancePage.vue") },
        { path: "settings/system/migrations", name: "settings-migrations", component: () => import("@/pages/settings/DatabaseMigrationPage.vue") },
        { path: "settings/system/plugins", name: "settings-plugins", component: () => import("@/pages/settings/PluginManagePage.vue") },
        { path: "settings/system/webhooks", name: "settings-webhooks", component: () => import("@/pages/settings/WebhooksPage.vue") },
        { path: "settings/projects", name: "settings-projects", component: () => import("@/pages/settings/ProjectsPage.vue") },
        { path: "settings/auth/pats", name: "settings-pats", component: () => import("@/pages/settings/PersonalAccessTokensPage.vue") },

        // ── 监控 ──
        { path: "monitor/server-info", name: "monitor-server", component: () => import("@/pages/monitor/ServerInfoPage.vue") },
        { path: "monitor/message-queue", name: "monitor-mq", component: () => import("@/pages/monitor/MessageQueuePage.vue") },
        { path: "monitor/scheduled-jobs", name: "monitor-jobs", component: () => import("@/pages/monitor/ScheduledJobsPage.vue") },

        // ── 安全与合规 ──
        { path: "assets", name: "assets", component: () => import("@/pages/AssetsPage.vue") },
        { path: "audit", name: "audit", component: () => import("@/pages/AuditPage.vue") },
        { path: "alert", name: "alert", component: () => import("@/pages/AlertPage.vue") },
        { path: "settings/license", name: "license", component: () => import("@/pages/LicensePage.vue") },

        // ── 应用工作区（使用 AppWorkspaceLayout） ──
        {
          path: "apps/:appId",
          component: () => import("@/layouts/AppWorkspaceLayout.vue"),
          children: [
            { path: "", name: "app-workspace", redirect: (to) => { const raw = to.params.appId; const appId = Array.isArray(raw) ? raw[0] : raw; return { path: `/apps/${appId ?? ""}/dashboard` }; } },
            { path: "dashboard", name: "app-dashboard", component: () => import("@/pages/apps/AppDashboardPage.vue") },
            { path: "settings", name: "app-settings", component: () => import("@/pages/apps/AppSettingsPage.vue") },
            { path: "pages", name: "app-pages", component: () => import("@/pages/apps/AppPagesPage.vue") },
            { path: "builder", name: "app-builder", component: () => import("@/pages/apps/AppBuilderPage.vue") },
            { path: "data", name: "app-data", component: () => import("@/pages/dynamic/DynamicTablesPage.vue") },
            { path: "data/erd", name: "app-data-erd", component: () => import("@/pages/dynamic/ERDCanvasPage.vue") },
            { path: "data/designer", name: "app-data-designer", component: () => import("@/pages/dynamic/DataDesignerPage.vue") },
            { path: "logic-flow", name: "app-logic-flows", component: () => import("@/pages/logic-flow/FlowDesignerPage.vue") },
            { path: "logic-flow/:id/designer", name: "app-logic-flow-designer", component: () => import("@/pages/logic-flow/FlowDesignerPage.vue") },
            { path: "flows", name: "app-approval-flows", component: () => import("@/pages/approval/ApprovalFlowsPage.vue") },
            { path: "workflows", name: "app-workflows", component: () => import("@/pages/workflow/WorkflowListPage.vue") },
            { path: "workflows/:id/editor", name: "app-workflow-editor", component: () => import("@/pages/workflow/WorkflowEditorPage.vue") }
          ]
        },

        // ── 审批 ──
        { path: "approval/designer/:id?", name: "approval-designer", component: () => import("@/pages/approval/ApprovalDesignerPage.vue") },
        { path: "approval/flows", name: "approval-flows", component: () => import("@/pages/approval/ApprovalFlowsPage.vue") },

        // ── AI ──
        { path: "ai/agents", name: "ai-agents", component: () => import("@/pages/ai/AgentListPage.vue") },
        { path: "ai/agents/:agentId/chat", name: "ai-agent-chat", component: () => import("@/pages/ai/AgentChatPage.vue") },
        { path: "ai/agents/:id/edit", name: "ai-agent-edit", component: () => import("@/pages/ai/AgentEditorPage.vue") },
        { path: "ai/model-configs", name: "ai-model-configs", component: () => import("@/pages/ai/ModelConfigsPage.vue") },
        { path: "ai/knowledge-bases", name: "ai-knowledge-bases", component: () => import("@/pages/ai/KnowledgeBasesPage.vue") },
        { path: "ai/plugins", name: "ai-plugins", component: () => import("@/pages/ai/AiPluginListPage.vue") },
        { path: "ai/marketplace", name: "ai-marketplace", component: () => import("@/pages/ai/AiMarketplacePage.vue") },
        { path: "admin/ai-config", name: "admin-ai-config", component: () => import("@/pages/ai/AiConfigPage.vue") },

        // ── 低代码（仅保留应用列表入口，指向应用目录） ──
        { path: "lowcode/apps", name: "lowcode-app-list", redirect: "/console/catalog" },

        // ── 可视化（功能开发中） ──
        { path: "visualization/center", name: "visualization-center", component: Placeholder, props: { titleKey: "route.visualizationCenter", descriptionKey: "featurePlaceholder.comingSoonDesc", backPath: "/console" } },
        { path: "visualization/designer", name: "visualization-designer", component: Placeholder, props: { titleKey: "route.visualizationDesigner", descriptionKey: "featurePlaceholder.comingSoonDesc", backPath: "/console" } }
      ]
    },
    { path: "/:pathMatch(.*)*", name: "not-found", redirect: "/console" }
  ]
});

router.getRoutes().forEach((routeRecord) => {
  const routeMeta = (routeRecord.meta ?? {}) as Record<string, unknown>;
  applyPermissionMetaToRoutePath(routeRecord.path, routeMeta);
  routeRecord.meta = routeMeta;
});

router.beforeEach(async (to, _from, next) => {
  const toMeta = (to.meta ?? {}) as Record<string, unknown>;
  applyPermissionMetaToRoutePath(to.path, toMeta);
  to.meta = toMeta;

  if (!setupChecked) {
    try {
      const resp = await getSetupState();
      setupReady = resp.success && resp.data?.status === "Ready";
    } catch {
      setupReady = false;
    }
    setupChecked = true;
  }

  if (!setupReady && to.name !== "setup") {
    next({ name: "setup" });
    return;
  }

  if (setupReady && to.name === "setup") {
    next({ path: "/login" });
    return;
  }

  const token = getAccessToken();
  if (to.meta.requiresAuth !== false && !token) {
    next({ name: "login", query: { redirect: to.fullPath } });
  } else if (to.name === "login" && token) {
    next({ path: "/console" });
  } else {
    next();
  }
});

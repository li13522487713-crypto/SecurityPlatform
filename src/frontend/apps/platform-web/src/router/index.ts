import { createRouter, createWebHistory } from "vue-router";
import { getAccessToken } from "@atlas/shared-core";

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: "/login",
      name: "login",
      component: () => import("@/pages/LoginPage.vue"),
      meta: { requiresAuth: false }
    },
    {
      path: "/register",
      name: "register",
      component: () => import("@/pages/RegisterPlaceholder.vue"),
      meta: { requiresAuth: false }
    },
    {
      path: "/",
      redirect: "/console"
    },
    {
      path: "/",
      component: () => import("@/layouts/ConsoleLayout.vue"),
      meta: { requiresAuth: true },
      children: [
        {
          path: "console",
          name: "console-home",
          component: () => import("@/pages/HomePage.vue")
        },
        {
          path: "profile",
          name: "profile",
          component: () => import("@/pages/ProfilePage.vue")
        },
        {
          path: "console/catalog",
          name: "console-catalog",
          component: () => import("@/pages/console/ApplicationCatalogPage.vue")
        },
        {
          path: "console/tenant-applications",
          name: "console-tenant-apps",
          component: () => import("@/pages/console/TenantApplicationsPage.vue")
        },
        {
          path: "console/runtime-contexts",
          name: "console-runtime-contexts",
          component: () => import("@/pages/console/RuntimeContextsPage.vue")
        },
        {
          path: "console/runtime-executions",
          name: "console-runtime-executions",
          component: () => import("@/pages/console/RuntimeExecutionsPage.vue")
        },
        {
          path: "console/resources",
          name: "console-resources",
          component: () => import("@/pages/console/ResourceCenterPage.vue")
        },
        {
          path: "console/releases",
          name: "console-releases",
          component: () => import("@/pages/console/ReleaseCenterPage.vue")
        },
        {
          path: "console/migration-governance",
          name: "console-migration-governance",
          component: () => import("@/pages/console/MigrationGovernancePage.vue")
        },
        // 系统管理
        {
          path: "settings/org/tenants",
          name: "sys-tenants",
          component: () => import("@/pages/system/TenantsPage.vue")
        },
        {
          path: "settings/org/users",
          name: "sys-users",
          component: () => import("@/pages/system/UsersPage.vue")
        },
        {
          path: "settings/org/departments",
          name: "sys-departments",
          component: () => import("@/pages/system/DepartmentsPage.vue")
        },
        {
          path: "settings/org/positions",
          name: "sys-positions",
          component: () => import("@/pages/system/PositionsPage.vue")
        },
        {
          path: "settings/auth/roles",
          name: "sys-roles",
          component: () => import("@/pages/system/RolesPage.vue")
        },
        {
          path: "settings/auth/menus",
          name: "sys-menus",
          component: () => import("@/pages/system/MenusPage.vue")
        },
        {
          path: "system/notifications",
          name: "sys-notifications",
          component: () => import("@/pages/system/NotificationsPage.vue")
        },
        {
          path: "system/login-logs",
          name: "sys-login-logs",
          component: () => import("@/pages/system/LoginLogsPage.vue")
        },
        {
          path: "system/online-users",
          name: "sys-online-users",
          component: () => import("@/pages/system/OnlineUsersPage.vue")
        },
        // 设置
        {
          path: "settings/system/dict-types",
          name: "settings-dict",
          component: () => import("@/pages/settings/DictTypesPage.vue")
        },
        {
          path: "settings/system/datasources",
          name: "settings-datasources",
          component: () => import("@/pages/settings/DataSourcesPage.vue")
        },
        {
          path: "settings/system/configs",
          name: "settings-configs",
          component: () => import("@/pages/settings/SystemConfigsPage.vue")
        },
        {
          path: "settings/system/plugins",
          name: "settings-plugins",
          component: () => import("@/pages/settings/PluginManagePage.vue")
        },
        {
          path: "settings/system/webhooks",
          name: "settings-webhooks",
          component: () => import("@/pages/settings/WebhooksPage.vue")
        },
        {
          path: "settings/projects",
          name: "settings-projects",
          component: () => import("@/pages/settings/ProjectsPage.vue")
        },
        {
          path: "settings/auth/pats",
          name: "settings-pats",
          component: () => import("@/pages/settings/PersonalAccessTokensPage.vue")
        },
        // 监控
        {
          path: "monitor/server-info",
          name: "monitor-server",
          component: () => import("@/pages/monitor/ServerInfoPage.vue")
        },
        {
          path: "monitor/message-queue",
          name: "monitor-mq",
          component: () => import("@/pages/monitor/MessageQueuePage.vue")
        },
        {
          path: "monitor/scheduled-jobs",
          name: "monitor-jobs",
          component: () => import("@/pages/monitor/ScheduledJobsPage.vue")
        },
        // 安全
        {
          path: "assets",
          name: "assets",
          component: () => import("@/pages/AssetsPage.vue")
        },
        {
          path: "audit",
          name: "audit",
          component: () => import("@/pages/AuditPage.vue")
        },
        {
          path: "alert",
          name: "alert",
          component: () => import("@/pages/AlertPage.vue")
        },
        {
          path: "settings/license",
          name: "license",
          component: () => import("@/pages/LicensePage.vue")
        },
        {
          path: "apps/:appId",
          name: "app-workspace",
          redirect: (to) => {
            const raw = to.params.appId;
            const appId = Array.isArray(raw) ? raw[0] : raw;
            return { path: `/apps/${appId ?? ""}/dashboard` };
          }
        },
        { path: "apps/:appId/dashboard", name: "app-dashboard", component: () => import("@/pages/apps/AppDashboardPage.vue") },
        { path: "apps/:appId/settings", name: "app-settings", component: () => import("@/pages/apps/AppSettingsPage.vue") },
        { path: "apps/:appId/pages", name: "app-pages", component: () => import("@/pages/apps/AppPagesPage.vue") },
        { path: "apps/:appId/builder", name: "app-builder", component: () => import("@/pages/apps/AppBuilderPage.vue") },
        { path: "apps/:appId/data", name: "app-data", component: () => import("@/pages/dynamic/DynamicTablesPage.vue") },
        { path: "apps/:appId/data/erd", name: "app-data-erd", component: () => import("@/pages/dynamic/ERDCanvasPage.vue") },
        { path: "apps/:appId/data/designer", name: "app-data-designer", component: () => import("@/pages/dynamic/DataDesignerPage.vue") },
        { path: "apps/:appId/logic-flow", name: "app-logic-flows", component: () => import("@/pages/logic-flow/FlowListPage.vue") },
        { path: "apps/:appId/logic-flow/:id/designer", name: "app-logic-flow-designer", component: () => import("@/pages/logic-flow/FlowDesignerPage.vue") },
        { path: "apps/:appId/flows", name: "app-approval-flows", component: () => import("@/pages/approval/ApprovalFlowsPage.vue") },
        { path: "approval/designer/:id?", name: "approval-designer", component: () => import("@/pages/approval/ApprovalDesignerPage.vue") },
        { path: "apps/:appId/workflows", name: "app-workflows", component: () => import("@/pages/workflow/WorkflowListPage.vue") },
        { path: "apps/:appId/workflows/:id/editor", name: "app-workflow-editor", component: () => import("@/pages/workflow/WorkflowEditorPage.vue") },
        { path: "ai/agents", name: "ai-agents", component: () => import("@/pages/ai/AgentListPage.vue") },
        { path: "ai/agents/:agentId/chat", name: "ai-agent-chat", component: () => import("@/pages/ai/AgentChatPage.vue") },
        { path: "ai/agents/:id/edit", name: "ai-agent-edit", component: () => import("@/pages/ai/AgentEditorPage.vue") },
        { path: "ai/model-configs", name: "ai-model-configs", component: () => import("@/pages/ai/ModelConfigsPage.vue") },
        { path: "ai/workspace", name: "ai-workspace", component: () => import("@/pages/ai/AiWorkspacePage.vue") },
        { path: "ai/library", name: "ai-library", component: () => import("@/pages/ai/AiLibraryPage.vue") },
        { path: "ai/knowledge-bases", name: "ai-knowledge-bases", component: () => import("@/pages/ai/KnowledgeBasesPage.vue") },
        { path: "ai/workflows", name: "ai-workflows", component: () => import("@/pages/ai/AiWorkflowListPage.vue") },
        { path: "ai/workflows/:id/edit", name: "ai-workflow-edit", component: () => import("@/pages/ai/AiWorkflowEditorPage.vue") },
        { path: "ai/plugins", name: "ai-plugins", component: () => import("@/pages/ai/AiPluginListPage.vue") },
        { path: "ai/multi-agent", name: "ai-multi-agent", component: () => import("@/pages/ai/MultiAgentListPage.vue") },
        { path: "ai/marketplace", name: "ai-marketplace", component: () => import("@/pages/ai/AiMarketplacePage.vue") },
        { path: "admin/ai-config", name: "admin-ai-config", component: () => import("@/pages/ai/AiConfigPage.vue") }
      ]
    },
    {
      path: "/:pathMatch(.*)*",
      name: "not-found",
      redirect: "/console"
    }
  ]
});

router.beforeEach((to, _from, next) => {
  const token = getAccessToken();
  if (to.meta.requiresAuth !== false && !token) {
    next({ name: "login", query: { redirect: to.fullPath } });
  } else if (to.name === "login" && token) {
    next({ path: "/console" });
  } else {
    next();
  }
});

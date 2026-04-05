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
        { path: "apps/:appId/data/workbench", name: "app-data-workbench", component: () => import("@/pages/dynamic/DynamicDataWorkbenchPage.vue") },
        { path: "apps/:appId/data/crud-config", name: "app-data-crud-config", component: () => import("@/pages/dynamic/CrudConfigPage.vue") },
        { path: "apps/:appId/data/preview", name: "app-data-preview", component: () => import("@/pages/dynamic/DataPreviewPage.vue") },
        { path: "apps/:appId/data/ddl-preview", name: "app-data-ddl-preview", component: () => import("@/pages/dynamic/DdlPreviewPage.vue") },
        { path: "apps/:appId/data/records", name: "app-data-records", component: () => import("@/pages/dynamic/DynamicRecordsNativePage.vue") },
        { path: "apps/:appId/data/table-crud", name: "app-data-table-crud", component: () => import("@/pages/dynamic/DynamicTableCrudPage.vue") },
        { path: "apps/:appId/data/approval-binding", name: "app-data-approval-binding", component: () => import("@/pages/dynamic/ApprovalBindingPage.vue") },
        { path: "apps/:appId/data/relation-design", name: "app-data-relation-design", component: () => import("@/pages/dynamic/RelationDesignPage.vue") },
        { path: "apps/:appId/data/logical-view", name: "app-data-logical-view", component: () => import("@/pages/dynamic/LogicalViewDesignPage.vue") },
        { path: "apps/:appId/data/view-designer", name: "app-data-view-designer", component: () => import("@/pages/dynamic/ViewDesignerPage.vue") },
        { path: "apps/:appId/data/schema-change", name: "app-data-schema-change", component: () => import("@/pages/dynamic/SchemaChangePage.vue") },
        { path: "apps/:appId/data/schema-snapshots", name: "app-data-schema-snapshots", component: () => import("@/pages/dynamic/SchemaSnapshotsPage.vue") },
        { path: "apps/:appId/data/impact-analysis", name: "app-data-impact-analysis", component: () => import("@/pages/dynamic/ImpactAnalysisPanel.vue") },
        { path: "apps/:appId/data/migration-preview", name: "app-data-migration-preview", component: () => import("@/pages/dynamic/MigrationPreviewPanel.vue") },
        { path: "apps/:appId/logic-flow", name: "app-logic-flows", component: () => import("@/pages/logic-flow/FlowListPage.vue") },
        { path: "apps/:appId/logic-flow/:id/designer", name: "app-logic-flow-designer", component: () => import("@/pages/logic-flow/FlowDesignerPage.vue") },
        { path: "apps/:appId/logic-flow/designer", name: "app-logic-flow-designer-default", component: () => import("@/pages/logic-flow/LogicFlowDesignerPage.vue") },
        { path: "apps/:appId/logic-flow/backend-capability", name: "app-logic-flow-backend-capability", component: () => import("@/pages/logic-flow/BackendCapabilityStudioPage.vue") },
        { path: "apps/:appId/logic-flow/batch-monitor", name: "app-logic-flow-batch-monitor", component: () => import("@/pages/logic-flow/BatchMonitorPage.vue") },
        { path: "apps/:appId/logic-flow/batch-designer", name: "app-logic-flow-batch-designer", component: () => import("@/pages/logic-flow/BatchJobDesignerPage.vue") },
        { path: "apps/:appId/logic-flow/batch-dead-letter", name: "app-logic-flow-batch-dead-letter", component: () => import("@/pages/logic-flow/BatchDeadLetterPage.vue") },
        { path: "apps/:appId/logic-flow/execution-monitor", name: "app-logic-flow-execution-monitor", component: () => import("@/pages/logic-flow/FlowExecutionMonitorPage.vue") },
        { path: "apps/:appId/logic-flow/execution-detail", name: "app-logic-flow-execution-detail", component: () => import("@/pages/logic-flow/ExecutionDetailPage.vue") },
        { path: "apps/:appId/logic-flow/execution-timeline", name: "app-logic-flow-execution-timeline", component: () => import("@/pages/logic-flow/ExecutionTimelinePage.vue") },
        { path: "apps/:appId/logic-flow/formula-builder", name: "app-logic-flow-formula-builder", component: () => import("@/pages/logic-flow/FormulaBuilderPage.vue") },
        { path: "apps/:appId/logic-flow/function-designer", name: "app-logic-flow-function-designer", component: () => import("@/pages/logic-flow/FunctionDesignerPage.vue") },
        { path: "apps/:appId/logic-flow/node-panel", name: "app-logic-flow-node-panel", component: () => import("@/pages/logic-flow/NodePanelPage.vue") },
        { path: "apps/:appId/logic-flow/plugin-management", name: "app-logic-flow-plugin-management", component: () => import("@/pages/logic-flow/PluginManagementPage.vue") },
        { path: "apps/:appId/logic-flow/resource-governance", name: "app-logic-flow-resource-governance", component: () => import("@/pages/logic-flow/ResourceGovernancePage.vue") },
        { path: "apps/:appId/flows", name: "app-approval-flows", component: () => import("@/pages/approval/ApprovalFlowsPage.vue") },
        { path: "approval/designer/:id?", name: "approval-designer", component: () => import("@/pages/approval/ApprovalDesignerPage.vue") },
        { path: "approval/flow-manage", name: "approval-flow-manage", component: () => import("@/pages/approval/ApprovalFlowManagePage.vue") },
        { path: "approval/instances", name: "approval-instance-manage", component: () => import("@/pages/approval/ApprovalInstanceManagePage.vue") },
        { path: "approval/workspace", name: "approval-workspace-manage", component: () => import("@/pages/approval/ApprovalWorkspacePage.vue") },
        { path: "approval/tasks/pool", name: "approval-task-pool", component: () => import("@/pages/approval/ApprovalTaskPoolPage.vue") },
        { path: "approval/tasks/:id", name: "approval-task-detail", component: () => import("@/pages/approval/ApprovalTaskDetailPage.vue") },
        { path: "approval/start", name: "approval-start", component: () => import("@/pages/approval/ApprovalStartPage.vue") },
        { path: "approval/agents", name: "approval-agent-config", component: () => import("@/pages/approval/ApprovalAgentConfigPage.vue") },
        { path: "approval/department-leader", name: "approval-department-leader", component: () => import("@/pages/approval/ApprovalDepartmentLeaderPage.vue") },
        { path: "apps/:appId/workflows", name: "app-workflows", component: () => import("@/pages/workflow/WorkflowListPage.vue") },
        { path: "apps/:appId/workflows/:id/editor", name: "app-workflow-editor", component: () => import("@/pages/workflow/WorkflowEditorPage.vue") },
        { path: "ai/agents", name: "ai-agents", component: () => import("@/pages/ai/AgentListPage.vue") },
        { path: "ai/agents/:agentId/chat", name: "ai-agent-chat", component: () => import("@/pages/ai/AgentChatPage.vue") },
        { path: "ai/agents/:id/edit", name: "ai-agent-edit", component: () => import("@/pages/ai/AgentEditorPage.vue") },
        { path: "ai/agents/workspace-chat", name: "ai-agent-workspace-chat", component: () => import("@/pages/ai/AgentWorkspaceChatPage.vue") },
        { path: "ai/apps", name: "ai-app-list", component: () => import("@/pages/ai/AiAppListPage.vue") },
        { path: "ai/apps/editor", name: "ai-app-editor", component: () => import("@/pages/ai/AiAppEditorPage.vue") },
        { path: "ai/databases", name: "ai-database-list", component: () => import("@/pages/ai/AiDatabaseListPage.vue") },
        { path: "ai/databases/detail", name: "ai-database-detail", component: () => import("@/pages/ai/AiDatabaseDetailPage.vue") },
        { path: "ai/model-configs", name: "ai-model-configs", component: () => import("@/pages/ai/ModelConfigsPage.vue") },
        { path: "ai/workspace", name: "ai-workspace", component: () => import("@/pages/ai/AiWorkspacePage.vue") },
        { path: "ai/library", name: "ai-library", component: () => import("@/pages/ai/AiLibraryPage.vue") },
        { path: "ai/knowledge-bases", name: "ai-knowledge-bases", component: () => import("@/pages/ai/KnowledgeBasesPage.vue") },
        { path: "ai/knowledge-bases/list", name: "ai-knowledge-base-list", component: () => import("@/pages/ai/KnowledgeBaseListPage.vue") },
        { path: "ai/knowledge-bases/detail", name: "ai-knowledge-base-detail", component: () => import("@/pages/ai/KnowledgeBaseDetailPage.vue") },
        { path: "ai/knowledge-bases/test", name: "ai-knowledge-base-test", component: () => import("@/pages/ai/KnowledgeBaseTestPage.vue") },
        { path: "ai/workflows", name: "ai-workflows", component: () => import("@/pages/ai/AiWorkflowListPage.vue") },
        { path: "ai/workflows/:id/edit", name: "ai-workflow-edit", component: () => import("@/pages/ai/AiWorkflowEditorPage.vue") },
        { path: "ai/plugins", name: "ai-plugins", component: () => import("@/pages/ai/AiPluginListPage.vue") },
        { path: "ai/plugins/detail", name: "ai-plugin-detail", component: () => import("@/pages/ai/AiPluginDetailPage.vue") },
        { path: "ai/plugins/api-editor", name: "ai-plugin-api-editor", component: () => import("@/pages/ai/AiPluginApiEditorPage.vue") },
        { path: "ai/marketplace/detail", name: "ai-marketplace-detail", component: () => import("@/pages/ai/AiMarketplaceDetailPage.vue") },
        { path: "ai/mock-sets", name: "ai-mock-sets", component: () => import("@/pages/ai/AiMockSetsPage.vue") },
        { path: "ai/open-platform", name: "ai-open-platform", component: () => import("@/pages/ai/AiOpenPlatformPage.vue") },
        { path: "ai/prompt-library", name: "ai-prompt-library", component: () => import("@/pages/ai/AiPromptLibraryPage.vue") },
        { path: "ai/search-results", name: "ai-search-results", component: () => import("@/pages/ai/AiSearchResultsPage.vue") },
        { path: "ai/shortcuts", name: "ai-shortcuts", component: () => import("@/pages/ai/AiShortcutsPage.vue") },
        { path: "ai/test-sets", name: "ai-test-sets", component: () => import("@/pages/ai/AiTestSetsPage.vue") },
        { path: "ai/variables", name: "ai-variables", component: () => import("@/pages/ai/AiVariablesPage.vue") },
        { path: "ai/evaluation/reports", name: "ai-evaluation-reports", component: () => import("@/pages/ai/EvaluationReportPage.vue") },
        { path: "ai/evaluation/tasks", name: "ai-evaluation-tasks", component: () => import("@/pages/ai/EvaluationTaskPage.vue") },
        { path: "ai/user-memory-settings", name: "ai-user-memory-settings", component: () => import("@/pages/ai/UserMemorySettingsPage.vue") },
        { path: "ai/multi-agent", name: "ai-multi-agent", component: () => import("@/pages/ai/MultiAgentListPage.vue") },
        { path: "ai/multi-agent/runs", name: "ai-multi-agent-runs", component: () => import("@/pages/ai/multi-agent/AgentTeamRunPage.vue") },
        { path: "ai/multi-agent/runs/detail", name: "ai-multi-agent-runs-detail", component: () => import("@/pages/ai/multi-agent/AgentTeamRunDetailPage.vue") },
        { path: "ai/multi-agent/versions", name: "ai-multi-agent-versions", component: () => import("@/pages/ai/multi-agent/AgentTeamVersionPage.vue") },
        { path: "ai/multi-agent/debug", name: "ai-multi-agent-debug", component: () => import("@/pages/ai/multi-agent/AgentTeamDebugPage.vue") },
        { path: "ai/multi-agent/orchestrations", name: "ai-multi-agent-orchestrations", component: () => import("@/pages/ai/multi-agent/MultiAgentOrchestrationListPage.vue") },
        { path: "ai/multi-agent/orchestrations/detail", name: "ai-multi-agent-orchestrations-detail", component: () => import("@/pages/ai/multi-agent/MultiAgentOrchestrationDetailPage.vue") },
        { path: "ai/marketplace", name: "ai-marketplace", component: () => import("@/pages/ai/AiMarketplacePage.vue") },
        { path: "lowcode/apps", name: "lowcode-app-list", component: () => import("@/pages/lowcode/AppListPage.vue") },
        { path: "lowcode/builder", name: "lowcode-app-builder", component: () => import("@/pages/lowcode/AppBuilderPage.vue") },
        { path: "lowcode/forms", name: "lowcode-form-list", component: () => import("@/pages/lowcode/FormListPage.vue") },
        { path: "lowcode/forms/designer", name: "lowcode-form-designer", component: () => import("@/pages/lowcode/FormDesignerPage.vue") },
        { path: "lowcode/templates", name: "lowcode-template-market", component: () => import("@/pages/lowcode/TemplateMarketPage.vue") },
        { path: "lowcode/plugins", name: "lowcode-plugin-market", component: () => import("@/pages/lowcode/PluginMarketPage.vue") },
        { path: "lowcode/packages", name: "lowcode-packages", component: () => import("@/pages/lowcode/PackagesPage.vue") },
        { path: "lowcode/messages", name: "lowcode-message-center", component: () => import("@/pages/lowcode/MessageCenterPage.vue") },
        { path: "lowcode/process-monitor", name: "lowcode-process-monitor", component: () => import("@/pages/lowcode/ProcessMonitorPage.vue") },
        { path: "lowcode/writeback-monitor", name: "lowcode-writeback-monitor", component: () => import("@/pages/lowcode/WritebackMonitorPage.vue") },
        { path: "lowcode/dynamic-migrations", name: "lowcode-dynamic-migrations", component: () => import("@/pages/lowcode/DynamicMigrationsPage.vue") },
        { path: "lowcode/reports", name: "lowcode-reports", component: () => import("@/pages/lowcode/ReportsPage.vue") },
        { path: "lowcode/dashboards", name: "lowcode-dashboards", component: () => import("@/pages/lowcode/DashboardsPage.vue") },
        { path: "lowcode/ai-assistant", name: "lowcode-ai-assistant", component: () => import("@/pages/lowcode/AiAssistantPage.vue") },
        { path: "visualization/center", name: "visualization-center", component: () => import("@/pages/visualization/VisualizationCenterPage.vue") },
        { path: "visualization/designer", name: "visualization-designer", component: () => import("@/pages/visualization/VisualizationDesignerPage.vue") },
        { path: "visualization/governance", name: "visualization-governance", component: () => import("@/pages/visualization/VisualizationGovernancePage.vue") },
        { path: "visualization/runtime", name: "visualization-runtime", component: () => import("@/pages/visualization/VisualizationRuntimePage.vue") },
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

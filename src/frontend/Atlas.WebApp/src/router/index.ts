import { createRouter, createWebHistory } from "vue-router";
import { getAccessToken, getTenantId } from "@/utils/auth";
import { clearCurrentAppIdFromStorage, getCurrentAppIdFromStorage, setCurrentAppIdToStorage } from "@/utils/app-context";
import { useUserStore } from "@/stores/user";
import { usePermissionStore } from "@/stores/permission";
import { message } from "ant-design-vue";
import NProgress from "nprogress";
import "nprogress/nprogress.css";
import { applyDocumentTitle } from "@/utils/i18n-navigation";

NProgress.configure({ showSpinner: false });

interface ApiRequestErrorLike extends Error {
  status?: number;
  payload?: unknown;
}

const LoginPage = () => import("@/pages/LoginPage.vue");
const RegisterPage = () => import("@/pages/RegisterPage.vue");
const ProfilePage = () => import("@/pages/ProfilePage.vue");
const NotFoundPage = () => import("@/pages/NotFoundPage.vue");
const ConsolePage = () => import("@/pages/console/ConsolePage.vue");
const ApplicationCatalogPage = () => import("@/pages/console/ApplicationCatalogPage.vue");
const TenantApplicationsPage = () => import("@/pages/console/TenantApplicationsPage.vue");
const RuntimeContextsPage = () => import("@/pages/console/RuntimeContextsPage.vue");
const RuntimeExecutionsPage = () => import("@/pages/console/RuntimeExecutionsPage.vue");
const ReleaseCenterPage = () => import("@/pages/console/ReleaseCenterPage.vue");
const CozeDebugPage = () => import("@/pages/console/CozeDebugPage.vue");
const AppDashboardPage = () => import("@/pages/apps/AppDashboardPage.vue");
const AppSettingsPage = () => import("@/pages/apps/AppSettingsPage.vue");
const AppPagesPage = () => import("@/pages/apps/AppPagesPage.vue");
const AppFormsPage = () => import("@/pages/lowcode/FormListPage.vue");
const AppFlowsPage = () => import("@/pages/ApprovalFlowsPage.vue");
const AppDataPage = () => import("@/pages/dynamic/DynamicTablesPage.vue");
const AppUsersPage = () => import("@/pages/apps/AppUsersPage.vue");
const AppPermissionsPage = () => import("@/pages/system/PermissionsPage.vue");
const ModelConfigsPage = () => import("@/pages/ai/ModelConfigsPage.vue");
const AiVariablesPage = () => import("@/pages/ai/AiVariablesPage.vue");
const AiOpenPlatformPage = () => import("@/pages/ai/AiOpenPlatformPage.vue");
const AiWorkspacePage = () => import("@/pages/ai/AiWorkspacePage.vue");
const AiLibraryPage = () => import("@/pages/ai/AiLibraryPage.vue");
const AiTestSetsPage = () => import("@/pages/ai/AiTestSetsPage.vue");
const AiMockSetsPage = () => import("@/pages/ai/AiMockSetsPage.vue");
const AiShortcutsPage = () => import("@/pages/ai/AiShortcutsPage.vue");
const AiSearchResultsPage = () => import("@/pages/ai/AiSearchResultsPage.vue");
const AiMarketplacePage = () => import("@/pages/ai/AiMarketplacePage.vue");
const AiPromptLibraryPage = () => import("@/pages/ai/AiPromptLibraryPage.vue");
const AiPluginListPage = () => import("@/pages/ai/AiPluginListPage.vue");
const AiPluginDetailPage = () => import("@/pages/ai/AiPluginDetailPage.vue");
const AiPluginApiEditorPage = () => import("@/pages/ai/AiPluginApiEditorPage.vue");
const AgentListPage = () => import("@/pages/ai/AgentListPage.vue");
const AgentEditorPage = () => import("@/pages/ai/AgentEditorPage.vue");
const PageRuntimeRenderer = () => import("@/pages/runtime/PageRuntimeRenderer.vue");
const AppListPage = () => import("@/pages/lowcode/AppListPage.vue");
const AppBuilderPage = () => import("@/pages/lowcode/AppBuilderPage.vue");
const FormListPage = () => import("@/pages/lowcode/FormListPage.vue");
const FormDesignerPage = () => import("@/pages/lowcode/FormDesignerPage.vue");
const WritebackMonitorPage = () => import("@/pages/lowcode/WritebackMonitorPage.vue");
const TemplateMarketPage = () => import("@/pages/lowcode/TemplateMarketPage.vue");
const ApprovalInstanceDetailPage = () => import("@/pages/ApprovalInstanceDetailPage.vue");
const NotificationsPage = () => import("@/pages/system/NotificationsPage.vue");
const DictTypesPage = () => import("@/pages/system/DictTypesPage.vue");
const SystemConfigsPage = () => import("@/pages/system/SystemConfigsPage.vue");
const TenantDataSourcesPage = () => import("@/pages/system/TenantDataSourcesPage.vue");
const TenantsPage = () => import("@/pages/system/TenantsPage.vue");
const RolesPage = () => import("@/pages/system/RolesPage.vue");
const PluginMarketPage = () => import("@/pages/lowcode/PluginMarketPage.vue");
const PluginManagePage = () => import("@/pages/system/PluginManagePage.vue");
const WebhooksPage = () => import("@/pages/system/WebhooksPage.vue");
const MessageQueuePage = () => import("@/pages/monitor/MessageQueuePage.vue");
const ServerInfoPage = () => import("@/pages/monitor/ServerInfoPage.vue");
const ScheduledJobsPage = () => import("@/pages/monitor/ScheduledJobsPage.vue");
const LoginLogsPage = () => import("@/pages/system/LoginLogsPage.vue");
const OnlineUsersPage = () => import("@/pages/system/OnlineUsersPage.vue");
const NotificationManagePage = () => import("@/pages/system/NotificationManagePage.vue");
const LicensePage = () => import("@/pages/LicensePage.vue");
const ToolsAuthorizationPage = () => import("@/pages/console/ToolsAuthorizationPage.vue");
const WorkflowListPage = () => import("@/pages/workflow/WorkflowListPage.vue");
const WorkflowEditorPage = () => import("@/pages/workflow/WorkflowEditorPage.vue");
const ApprovalDesignerPage = () => import("@/pages/ApprovalDesignerPage.vue");
const ApprovalFlowManagePage = () => import("@/pages/ApprovalFlowManagePage.vue");
const ApprovalFlowsPage = () => import("@/pages/ApprovalFlowsPage.vue");
const ApprovalInstanceManagePage = () => import("@/pages/ApprovalInstanceManagePage.vue");
const ApprovalWorkspacePage = () => import("@/pages/ApprovalWorkspacePage.vue");
const DepartmentsPage = () => import("@/pages/system/DepartmentsPage.vue");
const PositionsPage = () => import("@/pages/system/PositionsPage.vue");
const UsersPage = () => import("@/pages/system/UsersPage.vue");
const MenusPage = () => import("@/pages/system/MenusPage.vue");
const ProjectsPage = () => import("@/pages/system/ProjectsPage.vue");
const AssetsPage = () => import("@/pages/AssetsPage.vue");
const AuditPage = () => import("@/pages/AuditPage.vue");
const AlertPage = () => import("@/pages/AlertPage.vue");
declare module "vue-router" {
  interface RouteMeta {
    requiresAuth?: boolean;
    requiresPermission?: string;
    title?: string;
    titleKey?: string;
    deprecatedMessage?: string;
  }
}

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: "/login", name: "login", component: LoginPage, meta: { title: "登录", titleKey: "route.login" } },
    { path: "/register", name: "register", component: RegisterPage, meta: { title: "注册", titleKey: "route.register" } },
    { path: "/profile", name: "profile", component: ProfilePage, meta: { requiresAuth: true, title: "个人中心", titleKey: "route.profile" } },
    { path: "/console", name: "console-home", component: ConsolePage, meta: { requiresAuth: true, title: "平台控制台", titleKey: "route.console", requiresPermission: "apps:view" } },
    { path: "/console/apps", name: "console-apps", component: ConsolePage, meta: { requiresAuth: true, title: "应用中心", titleKey: "route.consoleApps", requiresPermission: "apps:view" } },
    { path: "/console/catalog", name: "console-catalog", component: ApplicationCatalogPage, meta: { requiresAuth: true, title: "应用目录", titleKey: "route.consoleCatalog", requiresPermission: "apps:view" } },
    { path: "/console/tenant-applications", name: "console-tenant-applications", component: TenantApplicationsPage, meta: { requiresAuth: true, title: "租户开通", titleKey: "route.consoleTenantApplications", requiresPermission: "apps:view" } },
    { path: "/console/runtime-contexts", name: "console-runtime-contexts", component: RuntimeContextsPage, meta: { requiresAuth: true, title: "运行上下文", titleKey: "route.consoleRuntimeContexts", requiresPermission: "apps:view" } },
    { path: "/console/runtime-executions", name: "console-runtime-executions", component: RuntimeExecutionsPage, meta: { requiresAuth: true, title: "执行记录", titleKey: "route.consoleRuntimeExecutions", requiresPermission: "apps:view" } },
    { path: "/console/resources", name: "console-resources", component: ConsolePage, meta: { requiresAuth: true, title: "资源中心", titleKey: "route.consoleResources", requiresPermission: "apps:view" } },
    { path: "/console/releases", name: "console-releases", component: ReleaseCenterPage, meta: { requiresAuth: true, title: "发布中心", titleKey: "route.consoleReleases", requiresPermission: "apps:view" } },
    { path: "/console/debug", name: "console-debug-layer", component: CozeDebugPage, meta: { requiresAuth: true, title: "调试层", titleKey: "route.consoleDebugLayer", requiresPermission: "apps:view" } },
    { path: "/console/tools", name: "console-tools", component: ToolsAuthorizationPage, meta: { requiresAuth: true, title: "工具授权中心", titleKey: "route.consoleTools", requiresPermission: "system:admin" } },
    { path: "/console/datasources", name: "console-datasources", component: TenantDataSourcesPage, meta: { requiresAuth: true, title: "数据源管理", titleKey: "route.consoleDatasources", requiresPermission: "system:admin" } },
    { path: "/console/settings/system/configs", name: "console-system-configs", component: SystemConfigsPage, meta: { requiresAuth: true, title: "系统设置", titleKey: "route.consoleSystemConfigs", requiresPermission: "config:view" } },
    { path: "/apps/:appId", name: "app-workspace-root", redirect: to => `/apps/${to.params.appId}/dashboard`, meta: { requiresAuth: true, title: "应用工作台", titleKey: "route.appWorkspace", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/dashboard", name: "app-workspace-dashboard", component: AppDashboardPage, meta: { requiresAuth: true, title: "应用仪表盘", titleKey: "route.appDashboard", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/builder", name: "app-workspace-builder", component: AppBuilderPage, meta: { requiresAuth: true, title: "应用设计器", titleKey: "route.appBuilder", requiresPermission: "apps:update" } },
    { path: "/apps/:appId/settings", name: "app-workspace-settings", component: AppSettingsPage, meta: { requiresAuth: true, title: "应用设置", titleKey: "route.appSettings", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/pages", name: "app-workspace-pages", component: AppPagesPage, meta: { requiresAuth: true, title: "页面管理", titleKey: "route.appPages", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/forms", name: "app-workspace-forms", component: AppFormsPage, meta: { requiresAuth: true, title: "表单管理", titleKey: "route.forms", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/forms/:id/designer", name: "app-workspace-form-designer", component: FormDesignerPage, meta: { requiresAuth: true, title: "表单设计器", titleKey: "route.formDesigner", requiresPermission: "apps:update" } },
    { path: "/apps/:appId/flows", name: "app-workspace-flows", component: AppFlowsPage, meta: { requiresAuth: true, title: "流程管理", titleKey: "route.processManage", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/agents", name: "app-workspace-agents", component: AgentListPage, meta: { requiresAuth: true, title: "Agent 列表", titleKey: "route.aiAgentList", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/workflows", name: "app-workspace-workflows", component: WorkflowListPage, meta: { requiresAuth: true, title: "工作流列表", titleKey: "route.aiWorkflowList", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/prompts", name: "app-workspace-prompts", component: AiPromptLibraryPage, meta: { requiresAuth: true, title: "Prompt 模板", titleKey: "route.aiPromptTemplates", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/plugins", name: "app-workspace-plugins", component: AiPluginListPage, meta: { requiresAuth: true, title: "插件配置", titleKey: "route.aiPluginConfig", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/plugins/:id", name: "app-workspace-plugin-detail", component: AiPluginDetailPage, meta: { requiresAuth: true, title: "插件详情", titleKey: "route.aiPluginDetail", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/plugins/:id/apis/:apiId", name: "app-workspace-plugin-api-editor", component: AiPluginApiEditorPage, meta: { requiresAuth: true, title: "插件 API 编辑器", titleKey: "route.aiPluginApiEditor", requiresPermission: "apps:update" } },
    { path: "/apps/:appId/workflows/:id/editor", name: "app-workspace-workflow-editor", component: WorkflowEditorPage, meta: { requiresAuth: true, title: "工作流设计器", titleKey: "route.workflowEditor" } },
    { path: "/apps/:appId/agents/:id/edit", name: "app-workspace-agent-editor", component: AgentEditorPage, meta: { requiresAuth: true, title: "Agent 编辑", titleKey: "route.aiAgentEdit" } },
    { path: "/apps/:appId/data", name: "app-workspace-data", component: AppDataPage, meta: { requiresAuth: true, title: "数据管理", titleKey: "route.dataManage", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/users", name: "app-workspace-users", component: AppUsersPage, meta: { requiresAuth: true, title: "应用成员", titleKey: "route.appUsers", requiresPermission: "apps:members:view" } },
    { path: "/apps/:appId/permissions", name: "app-workspace-permissions", component: AppPermissionsPage, meta: { requiresAuth: true, title: "权限入口", titleKey: "route.permissionsEntry", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/run/:pageKey", name: "app-workspace-runtime", component: PageRuntimeRenderer, meta: { requiresAuth: true, title: "应用运行态", titleKey: "route.appRuntime", requiresPermission: "apps:view" } },
    { path: "/r/:appKey/:pageKey", name: "runtime-delivery-page", component: PageRuntimeRenderer, meta: { requiresAuth: true, title: "运行交付面", titleKey: "route.runtimeDelivery" } },
    { path: "/runtime/:appKey/:pageKey", name: "runtime-legacy", redirect: to => `/r/${to.params.appKey}/${to.params.pageKey}`, meta: { requiresAuth: true, title: "运行交付面(Deprecated)", titleKey: "route.runtimeDeliveryDeprecated", deprecatedMessage: "路由 /runtime/* 已迁移至 /r/*，请更新收藏和调用方。" } },
    { path: "/process/instances/:id", name: "process-instance-detail", component: ApprovalInstanceDetailPage, meta: { requiresAuth: true, title: "流程详情", titleKey: "route.processDetail", requiresPermission: "approval:flow:view" } },
    { path: "/system/notifications", name: "system-notifications", component: NotificationsPage, meta: { requiresAuth: true, title: "通知中心", titleKey: "route.notifications" } },
    { path: "/system/notifications/manage", name: "system-notifications-manage", component: NotificationManagePage, meta: { requiresAuth: true, title: "公告管理", titleKey: "route.notificationsManage", requiresPermission: "notification:manage" } },
    { path: "/notifications", name: "system-notifications-legacy", redirect: "/system/notifications", meta: { requiresAuth: true, title: "通知中心", titleKey: "route.notifications" } },
    { path: "/settings/system/dict-types", name: "settings-system-dict-types", component: DictTypesPage, meta: { requiresAuth: true, title: "字典管理", titleKey: "route.dictTypes", requiresPermission: "dict:type:view" } },
    { path: "/settings/system/datasources", name: "settings-system-datasources", component: TenantDataSourcesPage, meta: { requiresAuth: true, title: "数据源管理", titleKey: "route.datasources", requiresPermission: "system:admin" } },
    { path: "/settings/system/configs", name: "settings-system-configs", component: SystemConfigsPage, meta: { requiresAuth: true, title: "参数配置", titleKey: "route.systemConfigs", requiresPermission: "config:view" } },
    { path: "/settings/ai/model-configs", name: "settings-ai-model-configs", component: ModelConfigsPage, meta: { requiresAuth: true, title: "模型配置", titleKey: "route.modelConfigs" } },
    { path: "/ai/variables", name: "ai-variables-static", component: AiVariablesPage, meta: { requiresAuth: true, title: "变量管理", titleKey: "route.aiVariables" } },
    { path: "/ai/open-platform", name: "ai-open-platform-static", component: AiOpenPlatformPage, meta: { requiresAuth: true, title: "开放平台", titleKey: "route.aiOpenPlatform" } },
    { path: "/ai/workspace", name: "ai-workspace-static", component: AiWorkspacePage, meta: { requiresAuth: true, title: "AI 工作台", titleKey: "route.aiWorkspace" } },
    { path: "/ai/library", name: "ai-library-static", component: AiLibraryPage, meta: { requiresAuth: true, title: "资源库", titleKey: "route.aiLibrary" } },
    { path: "/ai/devops/test-sets", name: "ai-test-sets-static", component: AiTestSetsPage, meta: { requiresAuth: true, title: "测试集", titleKey: "route.aiTestSets" } },
    { path: "/ai/devops/mock-sets", name: "ai-mock-sets-static", component: AiMockSetsPage, meta: { requiresAuth: true, title: "Mock 集", titleKey: "route.aiMockSets" } },
    { path: "/ai/shortcuts", name: "ai-shortcuts-static", component: AiShortcutsPage, meta: { requiresAuth: true, title: "快捷命令", titleKey: "route.aiShortcuts" } },
    { path: "/ai/search", name: "ai-search-static", component: AiSearchResultsPage, meta: { requiresAuth: true, title: "统一搜索", titleKey: "route.aiSearch" } },
    { path: "/ai/marketplace", name: "ai-marketplace-static", component: AiMarketplacePage, meta: { requiresAuth: true, title: "应用市场", titleKey: "route.aiMarketplace" } },
    {
      path: "/ai/agents",
      name: "ai-agent-list-static",
      redirect: to => buildWorkspaceRedirectPath(to, appId => `/apps/${appId}/agents`),
      meta: { requiresAuth: true, title: "Agent 列表(Deprecated)", titleKey: "route.aiAgentListDeprecated", deprecatedMessage: "旧路由 /ai/agents 已迁移至 /apps/:appId/agents。" }
    },
    {
      path: "/ai/agents/:id/edit",
      name: "ai-agent-edit-static",
      redirect: to => buildWorkspaceRedirectPath(to, appId => `/apps/${appId}/agents/${to.params.id}/edit`),
      meta: { requiresAuth: true, title: "Agent 编辑(Deprecated)", titleKey: "route.aiAgentEditDeprecated" }
    },
    {
      path: "/ai/workflows",
      name: "ai-workflow-list-legacy",
      redirect: to => buildWorkspaceRedirectPath(to, appId => `/apps/${appId}/workflows`),
      meta: { requiresAuth: true, title: "工作流列表(Deprecated)", titleKey: "route.aiWorkflowListDeprecated", deprecatedMessage: "旧路由 /ai/workflows 已迁移至 /apps/:appId/workflows。" }
    },
    {
      path: "/ai/prompts",
      name: "ai-prompt-list-legacy",
      redirect: to => buildWorkspaceRedirectPath(to, appId => `/apps/${appId}/prompts`),
      meta: { requiresAuth: true, title: "Prompt 模板(Deprecated)", titleKey: "route.aiPromptTemplatesDeprecated", deprecatedMessage: "旧路由 /ai/prompts 已迁移至 /apps/:appId/prompts。" }
    },
    {
      path: "/ai/plugins",
      name: "ai-plugin-list-legacy",
      redirect: to => buildWorkspaceRedirectPath(to, appId => `/apps/${appId}/plugins`),
      meta: { requiresAuth: true, title: "插件配置(Deprecated)", titleKey: "route.aiPluginConfigDeprecated", deprecatedMessage: "旧路由 /ai/plugins 已迁移至 /apps/:appId/plugins。" }
    },
    {
      path: "/ai/plugins/:id",
      name: "ai-plugin-detail-legacy",
      redirect: to => buildWorkspaceRedirectPath(to, appId => `/apps/${appId}/plugins/${to.params.id}`),
      meta: { requiresAuth: true, title: "插件详情(Deprecated)", titleKey: "route.aiPluginDetailDeprecated", deprecatedMessage: "旧路由 /ai/plugins/:id 已迁移至 /apps/:appId/plugins/:id。" }
    },
    {
      path: "/ai/plugins/:id/apis/:apiId",
      name: "ai-plugin-api-editor-legacy",
      redirect: to => buildWorkspaceRedirectPath(to, appId => `/apps/${appId}/plugins/${to.params.id}/apis/${to.params.apiId}`),
      meta: { requiresAuth: true, title: "插件 API 编辑器(Deprecated)", titleKey: "route.aiPluginApiEditorDeprecated", deprecatedMessage: "旧路由 /ai/plugins/:id/apis/:apiId 已迁移至 /apps/:appId/plugins/:id/apis/:apiId。" }
    },
    { path: "/settings/auth/roles", name: "SettingsAuthRoles", component: RolesPage, meta: { requiresAuth: true, title: "角色管理", titleKey: "route.roles", requiresPermission: "roles:view" } },
    { path: "/lowcode/plugin-market", name: "plugin-market", component: PluginMarketPage, meta: { requiresAuth: true, title: "插件市场", titleKey: "route.pluginMarket" } },
    { path: "/settings/system/plugins", name: "settings-plugins", component: PluginManagePage, meta: { requiresAuth: true, title: "插件管理", titleKey: "route.plugins", requiresPermission: "system:admin" } },
    { path: "/settings/system/webhooks", name: "settings-webhooks", component: WebhooksPage, meta: { requiresAuth: true, title: "Webhook 管理", titleKey: "route.webhooks" } },
    { path: "/monitor/message-queue", name: "monitor-message-queue", component: MessageQueuePage, meta: { requiresAuth: true, title: "消息队列监控", titleKey: "route.messageQueue", requiresPermission: "system:admin" } },
    { path: "/monitor/server-info", name: "monitor-server-info", component: ServerInfoPage, meta: { requiresAuth: true, title: "服务器监控", titleKey: "route.serverInfo", requiresPermission: "system:admin" } },
    { path: "/monitor/scheduled-jobs", name: "monitor-scheduled-jobs", component: ScheduledJobsPage, meta: { requiresAuth: true, title: "定时任务", titleKey: "route.scheduledJobs" } },
    { path: "/system/login-logs", name: "system-login-logs", component: LoginLogsPage, meta: { requiresAuth: true, title: "登录日志", titleKey: "route.loginLogs", requiresPermission: "system:admin" } },
    { path: "/system/online-users", name: "system-online-users", component: OnlineUsersPage, meta: { requiresAuth: true, title: "在线用户", titleKey: "route.onlineUsers" } },

    { path: "/settings/license", name: "settings-license", component: LicensePage, meta: { requiresAuth: true, title: "授权管理", titleKey: "route.license", requiresPermission: "system:license:view" } },
    {
      path: "/settings/:pathMatch(.*)*",
      name: "settings-legacy",
      redirect: (to) => {
        const pathMatch = to.params.pathMatch;
        const suffix = Array.isArray(pathMatch)
          ? pathMatch.join("/")
          : typeof pathMatch === "string"
            ? pathMatch
            : "";
        return `/console/settings/${suffix}`;
      },
      meta: { requiresAuth: true, title: "兼容设置路由（Deprecated）", titleKey: "route.settingsLegacyDeprecated" }
    },
    { path: "/system/dict-types", name: "system-dict-types-legacy", redirect: "/settings/system/dict-types", meta: { requiresAuth: true, title: "字典管理", titleKey: "route.dictTypes" } },
    { path: "/system/configs", name: "system-configs-legacy", redirect: "/settings/system/configs", meta: { requiresAuth: true, title: "参数配置", titleKey: "route.systemConfigs" } },
    { path: "/alerts", name: "alerts-legacy", redirect: "/alert", meta: { requiresAuth: true, title: "告警", titleKey: "route.alertLegacy" } },
    { path: "/coze/debug", name: "coze-debug-legacy", redirect: "/console/debug", meta: { requiresAuth: true, title: "调试层(Deprecated)", titleKey: "route.consoleDebugLayerDeprecated", deprecatedMessage: "路由 /coze/debug 已迁移至 /console/debug。" } },
    { path: "/lowcode/apps", name: "app-list", component: AppListPage, meta: { requiresAuth: true, title: "低代码应用", titleKey: "route.lowcodeApps", requiresPermission: "apps:view" } },
    {
      path: "/lowcode/apps/:id/builder",
      name: "app-builder",
      redirect: to => `/apps/${to.params.id}/builder`,
      meta: { requiresAuth: true, title: "应用设计器(Deprecated)", titleKey: "route.appBuilderDeprecated", requiresPermission: "apps:update", deprecatedMessage: "旧路由 /lowcode/apps/:id/builder 已迁移至 /apps/:id/builder。" }
    },
    { path: "/lowcode/forms", name: "apps-form-list", component: FormListPage, meta: { requiresAuth: true, title: "表单管理", titleKey: "route.forms", requiresPermission: "apps:view" } },
    { path: "/lowcode/templates", name: "template-market", component: TemplateMarketPage, meta: { requiresAuth: true, title: "模板市场", titleKey: "route.templateMarket", requiresPermission: "apps:view" } },
    {
      path: "/lowcode/forms/:id/designer",
      name: "apps-form-designer",
      redirect: to => buildWorkspaceRedirectPath(to, appId => `/apps/${appId}/forms/${to.params.id}/designer`),
      meta: { requiresAuth: true, title: "表单设计器(Deprecated)", titleKey: "route.formDesignerDeprecated", requiresPermission: "apps:update", deprecatedMessage: "旧路由 /lowcode/forms/:id/designer 已迁移至 /apps/:appId/forms/:id/designer。" }
    },
    { path: "/monitor/writeback-failures", name: "monitor-writeback-failures", component: WritebackMonitorPage, meta: { requiresAuth: true, title: "回写监控", titleKey: "route.writebackMonitor", requiresPermission: "system:admin" } },
    {
      path: "/workflow",
      name: "workflow-list",
      redirect: to => buildWorkspaceRedirectPath(to, appId => `/apps/${appId}/workflows`),
      meta: { requiresAuth: true, title: "工作流管理(Deprecated)", titleKey: "route.workflowListDeprecated", deprecatedMessage: "旧路由 /workflow 已迁移至 /apps/:appId/workflows。" }
    },
    {
      path: "/workflow/:id/editor",
      name: "workflow-editor",
      redirect: to => buildWorkspaceRedirectPath(to, appId => `/apps/${appId}/workflows/${to.params.id}/editor`),
      meta: { requiresAuth: true, title: "工作流设计器(Deprecated)", titleKey: "route.workflowEditorDeprecated", deprecatedMessage: "旧路由 /workflow/:id/editor 已迁移至 /apps/:appId/workflows/:id/editor。" }
    },
    {
      path: "/ai/workflows/:id/edit",
      name: "ai-workflow-editor-legacy",
      redirect: to => buildWorkspaceRedirectPath(to, appId => `/apps/${appId}/workflows/${to.params.id}/editor`),
      meta: { requiresAuth: true, title: "AI 工作流设计器(Deprecated)", titleKey: "route.aiWorkflowEditorDeprecated", deprecatedMessage: "旧路由 /ai/workflows/:id/edit 已迁移至 /apps/:appId/workflows/:id/editor。" }
    },
    { path: "/approval/designer", name: "approval-designer", component: ApprovalDesignerPage, meta: { requiresAuth: true, title: "流程设计器", titleKey: "route.approvalDesigner", requiresPermission: "approval:flow:create" } },
    { path: "/approval/flows/manage", name: "approval-flows-manage", component: ApprovalFlowManagePage, meta: { requiresAuth: true, title: "流程发布总览", titleKey: "route.approvalFlowManage", requiresPermission: "approval:flow:manage" } },
    { path: "/approval/flows", name: "approval-flows", component: ApprovalFlowsPage, meta: { requiresAuth: true, title: "流程定义列表", titleKey: "route.approvalFlows", requiresPermission: "approval:flow:view" } },
    { path: "/approval/instances/manage", name: "approval-instances-manage", component: ApprovalInstanceManagePage, meta: { requiresAuth: true, title: "所有审批实例", titleKey: "route.approvalInstancesManage", requiresPermission: "system:admin" } },
    { path: "/approval/workspace", name: "approval-workspace", component: ApprovalWorkspacePage, meta: { requiresAuth: true, title: "审批工作台", titleKey: "route.approvalWorkspace" } },
    { path: "/approval/instances", name: "approval-instances", redirect: "/approval/workspace?tab=requests", meta: { title: "我的申请(Deprecated)", titleKey: "route.approvalRequestsDeprecated" } },
    { path: "/approval/inbox", name: "approval-inbox", redirect: "/approval/workspace?tab=pending", meta: { title: "审批待办(Deprecated)", titleKey: "route.approvalInboxDeprecated" } },
    { path: "/settings/org/tenants", name: "settings-org-tenants", component: TenantsPage, meta: { requiresAuth: true, title: "租户管理", titleKey: "route.tenants", requiresPermission: "system:tenant:query" } },
    { path: "/settings/org/departments", name: "settings-org-deps", component: DepartmentsPage, meta: { requiresAuth: true, title: "组织架构", titleKey: "route.departments", requiresPermission: "departments:view" } },
    { path: "/settings/org/positions", name: "settings-org-positions", component: PositionsPage, meta: { requiresAuth: true, title: "职位名称", titleKey: "route.positions", requiresPermission: "positions:view" } },
    { path: "/settings/org/users", name: "settings-org-users", component: UsersPage, meta: { requiresAuth: true, title: "员工管理", titleKey: "route.users", requiresPermission: "users:view" } },
    { path: "/settings/auth/menus", name: "settings-auth-menus", component: MenusPage, meta: { requiresAuth: true, title: "菜单管理", titleKey: "route.menus", requiresPermission: "menus:view" } },
    { path: "/settings/projects", name: "settings-projects", component: ProjectsPage, meta: { requiresAuth: true, title: "项目管理", titleKey: "route.projects", requiresPermission: "projects:view" } },
    { path: "/assets", name: "assets-manage", component: AssetsPage, meta: { requiresAuth: true, title: "资产管理", titleKey: "route.assets", requiresPermission: "assets:view" } },
    { path: "/audit", name: "audit-manage", component: AuditPage, meta: { requiresAuth: true, title: "审计日志", titleKey: "route.audit", requiresPermission: "audit:view" } },
    { path: "/alert", name: "alert-manage", component: AlertPage, meta: { requiresAuth: true, title: "告警管理", titleKey: "route.alert", requiresPermission: "alert:view" } },
    { path: "/approval/tasks", name: "approval-tasks", redirect: "/approval/workspace?tab=pending", meta: { title: "我的待办(Deprecated)", titleKey: "route.approvalTasksDeprecated" } },
    { path: "/approval/done", redirect: "/approval/workspace?tab=done", meta: { title: "已办任务(Deprecated)", titleKey: "route.approvalDoneDeprecated" } },
    { path: "/approval/cc", redirect: "/approval/workspace?tab=cc", meta: { title: "我的抄送(Deprecated)", titleKey: "route.approvalCcDeprecated" } },
    { path: "/:pathMatch(.*)*", name: "not-found", component: NotFoundPage }
  ]
});

const whiteList = ["/login", "/register"];
const legacyRedirectNoticeCache = new Set<string>();

function isPrivilegedUser(userStore: ReturnType<typeof useUserStore>) {
  if (userStore.profile?.isPlatformAdmin) {
    return true;
  }

  return userStore.permissions.includes("*:*:*")
    || userStore.roles.some((role: string) => ["admin", "superadmin"].includes(role.toLowerCase()));
}

function hasPermission(userStore: ReturnType<typeof useUserStore>, permission: string) {
  return userStore.permissions.includes(permission) || isPrivilegedUser(userStore);
}

function getAuthFallbackPath(userStore: ReturnType<typeof useUserStore>) {
  if (hasPermission(userStore, "apps:view")) {
    return "/console";
  }
  if (hasPermission(userStore, "approval:flow:view")) {
    return "/approval/flows";
  }
  if (hasPermission(userStore, "users:view")) {
    return "/settings/org/users";
  }
  if (hasPermission(userStore, "audit:view")) {
    return "/audit";
  }
  return "/system/notifications";
}

function resolveLegacyAppId(to: { query: Record<string, unknown>; params: Record<string, unknown> }): string | null {
  const queryAppId = typeof to.query.appId === "string" ? to.query.appId : null;
  const paramAppId = typeof to.params.appId === "string" ? to.params.appId : null;
  const cachedAppId = getCurrentAppIdFromStorage();
  const selected = queryAppId || paramAppId || cachedAppId;
  if (!selected || !selected.trim()) {
    return null;
  }

  return selected.trim();
}

function buildWorkspaceRedirectPath(
  to: { query: Record<string, unknown>; params: Record<string, unknown> },
  suffixBuilder: (appId: string) => string
): string {
  const appId = resolveLegacyAppId(to);
  if (!appId) {
    return "/console/apps";
  }

  return suffixBuilder(appId);
}

function syncAppContextFromRoute(to: { params: Record<string, unknown>; path: string }) {
  const routeAppId = typeof to.params.appId === "string" ? to.params.appId.trim() : "";
  if (routeAppId) {
    setCurrentAppIdToStorage(routeAppId);
    return;
  }

  if (!to.path.startsWith("/apps/")) {
    clearCurrentAppIdFromStorage();
  }
}

function notifyLegacyRedirect(to: { redirectedFrom?: { path: string; meta: Record<string, unknown> } | undefined }) {
  const redirectedFrom = to.redirectedFrom;
  if (!redirectedFrom) {
    return;
  }
  const deprecatedMessage = redirectedFrom.meta?.deprecatedMessage;
  if (typeof deprecatedMessage !== "string" || !deprecatedMessage.trim()) {
    return;
  }
  if (legacyRedirectNoticeCache.has(redirectedFrom.path)) {
    return;
  }
  message.warning(deprecatedMessage);
  legacyRedirectNoticeCache.add(redirectedFrom.path);
}

router.beforeEach(async (to, from, next) => {
  NProgress.start();

  const token = getAccessToken();
  const tenantId = getTenantId();
  const userStore = useUserStore();
  const permissionStore = usePermissionStore();

  if (token && tenantId) {
    if (to.path === "/login") {
      next({ path: getAuthFallbackPath(userStore), replace: true });
      NProgress.done();
      return;
    }

    if (!permissionStore.routeLoaded || !userStore.profile) {
      try {
        await userStore.getInfo();
        await permissionStore.generateRoutes();
        permissionStore.registerRoutes(router);
        // 仅按 URL 重放，避免携带 not-found 名称导致动态路由注册后仍落入 404。
        next({
          path: to.path,
          query: to.query,
          hash: to.hash,
          replace: true
        });
        return;
      } catch (err) {
        console.error(err);
        await userStore.logout();
        const requestError = err as ApiRequestErrorLike;
        const alreadyHandledByApiCore = typeof requestError?.status === "number" || requestError?.payload !== undefined;
        if (!alreadyHandledByApiCore) {
          // 仅兜底提示非 API 异常，避免与 api-core 全局错误提示重复弹窗。
          message.error(requestError?.message || "登录失败，请重新登录");
        }
        next({ path: "/login" });
        NProgress.done();
        return;
      }
    }

    if (to.meta.requiresPermission && typeof to.meta.requiresPermission === "string") {
      const has = hasPermission(userStore, to.meta.requiresPermission);
      if (!has) {
        if (to.path === "/console" || to.path.startsWith("/console/")) {
          // 控制台页面通过布局层统一展示“暂无访问权限”空状态，避免强制重定向打断导航语义。
          next();
          NProgress.done();
          return;
        }
        const fallbackPath = getAuthFallbackPath(userStore);
        if (fallbackPath !== to.path) {
          next({ path: fallbackPath, replace: true });
        } else {
          next({ path: "/system/notifications", replace: true });
        }
        NProgress.done();
        return;
      }
    }

    syncAppContextFromRoute(to);
    notifyLegacyRedirect(to);
    next();
  } else {
    if (whiteList.includes(to.path)) {
      next();
    } else {
      next(`/login?redirect=${encodeURIComponent(to.fullPath)}`);
      NProgress.done();
    }
  }
});

router.afterEach(() => {
  applyDocumentTitle(router.currentRoute.value);
  NProgress.done();
});

export default router;

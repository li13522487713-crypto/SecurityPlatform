import { createRouter, createWebHistory } from "vue-router";
import { getAccessToken, getTenantId } from "@/utils/auth";
import { clearCurrentAppIdFromStorage, getCurrentAppIdFromStorage, setCurrentAppIdToStorage } from "@/utils/app-context";
import { useUserStore } from "@/stores/user";
import { usePermissionStore } from "@/stores/permission";
import { message } from "ant-design-vue";
import NProgress from "nprogress";
import "nprogress/nprogress.css";
import { applyDocumentTitle } from "@/utils/i18n-navigation";
import { translate } from "@/i18n";

NProgress.configure({ showSpinner: false });

interface ApiRequestErrorLike extends Error {
  status?: number;
  payload?: unknown;
  code?: string;
  isNetworkError?: boolean;
}

function isNetworkErrorLike(error: unknown): boolean {
  const requestError = error as ApiRequestErrorLike | undefined;
  return Boolean(requestError?.isNetworkError);
}

function isAuthTerminalErrorLike(error: unknown): boolean {
  const requestError = error as ApiRequestErrorLike | undefined;
  if (!requestError) {
    return false;
  }
  if (requestError.status === 401) {
    return true;
  }
  const payloadCode = (requestError.payload as { code?: string } | undefined)?.code;
  const code = payloadCode ?? requestError.code ?? "";
  return code === "ACCOUNT_LOCKED" || code === "PASSWORD_EXPIRED";
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
const ResourceCenterPage = () => import("@/pages/console/ResourceCenterPage.vue");
const DataSourceConsumptionPage = () => import("@/pages/console/DataSourceConsumptionPage.vue");
const ReleaseCenterPage = () => import("@/pages/console/ReleaseCenterPage.vue");
const CozeDebugPage = () => import("@/pages/console/CozeDebugPage.vue");
const MigrationGovernancePage = () => import("@/pages/console/MigrationGovernancePage.vue");
const AppDatabaseMigrationPage = () => import("@/pages/console/AppDatabaseMigrationPage.vue");
const AppDashboardPage = () => import("@/pages/apps/AppDashboardPage.vue");
const AppSettingsPage = () => import("@/pages/apps/AppSettingsPage.vue");
const AppPagesPage = () => import("@/pages/apps/AppPagesPage.vue");
const AppFormsPage = () => import("@/pages/lowcode/FormListPage.vue");
const AppFlowsPage = () => import("@/pages/ApprovalFlowsPage.vue");
const AppDataPage = () => import("@/pages/dynamic/DynamicTablesPage.vue");
const ERDCanvasPage = () => import("@/pages/dynamic/ERDCanvasPage.vue");
const DynamicTableCrudPage = () => import("@/pages/dynamic/DynamicTableCrudPage.vue");
const DynamicRecordsNativePage = () => import("@/pages/dynamic/DynamicRecordsNativePage.vue");
const AppOrganizationPage = () => import("@/pages/apps/AppOrganizationPage.vue");
const AppPermissionsPage = () => import("@/pages/system/PermissionsPage.vue");
const ModelConfigsPage = () => import("@/pages/ai/ModelConfigsPage.vue");
const AiVariablesPage = () => import("@/pages/ai/AiVariablesPage.vue");
const AiOpenPlatformPage = () => import("@/pages/ai/AiOpenPlatformPage.vue");
const AiWorkspacePage = () => import("@/pages/ai/AiWorkspacePage.vue");
const AiLibraryPage = () => import("@/pages/ai/AiLibraryPage.vue");
const AiTestSetsPage = () => import("@/pages/ai/AiTestSetsPage.vue");
const EvaluationTaskPage = () => import("@/pages/ai/EvaluationTaskPage.vue");
const EvaluationReportPage = () => import("@/pages/ai/EvaluationReportPage.vue");
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
const UserMemorySettingsPage = () => import("@/pages/ai/UserMemorySettingsPage.vue");
const KnowledgeBaseListPage = () => import("@/pages/ai/KnowledgeBaseListPage.vue");
const KnowledgeBaseDetailPage = () => import("@/pages/ai/KnowledgeBaseDetailPage.vue");
const KnowledgeBaseTestPage = () => import("@/pages/ai/KnowledgeBaseTestPage.vue");
const MultiAgentOrchestrationListPage = () => import("@/pages/ai/multi-agent/MultiAgentOrchestrationListPage.vue");
const MultiAgentOrchestrationDetailPage = () => import("@/pages/ai/multi-agent/MultiAgentOrchestrationDetailPage.vue");
const PageRuntimeRenderer = () => import("@/pages/runtime/PageRuntimeRenderer.vue");
const AppListPage = () => import("@/pages/lowcode/AppListPage.vue");
const CustomDesignerMockPage = () => import("@/pages/lowcode/CustomDesignerMockPage.vue");
const AppBuilderPage = () => import("@/pages/lowcode/AppBuilderPage.vue");
const FormListPage = () => import("@/pages/lowcode/FormListPage.vue");
const FormDesignerPage = () => import("@/pages/lowcode/FormDesignerPage.vue");
const WritebackMonitorPage = () => import("@/pages/lowcode/WritebackMonitorPage.vue");
const TemplateMarketPage = () => import("@/pages/lowcode/TemplateMarketPage.vue");
const ApprovalInstanceDetailPage = () => import("@/pages/ApprovalInstanceDetailPage.vue");
const NotificationsPage = () => import("@/pages/system/NotificationsPage.vue");
const DictTypesPage = () => import("@/pages/system/DictTypesPage.vue");
const SystemConfigsPage = () => import("@/pages/system/SystemConfigsPage.vue");
const AiConfigPage = () => import("@/pages/admin/AiConfigPage.vue");
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
const FileTransferDemoPage = () => import("@/pages/system/FileTransferDemoPage.vue");
const AssetsPage = () => import("@/pages/AssetsPage.vue");
const AuditPage = () => import("@/pages/AuditPage.vue");
const AlertPage = () => import("@/pages/AlertPage.vue");
declare module "vue-router" {
  interface RouteMeta {
    requiresAuth?: boolean;
    requiresPermission?: string;
    title?: string;
    titleKey?: string;
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
    { path: "/console/resources", name: "console-resources", component: ResourceCenterPage, meta: { requiresAuth: true, title: "资源中心", titleKey: "route.consoleResources", requiresPermission: "apps:view" } },
    { path: "/console/resources/datasource-consumption", name: "console-datasource-consumption", component: DataSourceConsumptionPage, meta: { requiresAuth: true, title: "数据源消费分析", titleKey: "route.consoleDatasourceConsumption", requiresPermission: "apps:view" } },
    { path: "/console/releases", name: "console-releases", component: ReleaseCenterPage, meta: { requiresAuth: true, title: "发布中心", titleKey: "route.consoleReleases", requiresPermission: "apps:view" } },
    { path: "/console/debug", name: "console-debug-layer", component: CozeDebugPage, meta: { requiresAuth: true, title: "调试层", titleKey: "route.consoleDebugLayer", requiresPermission: "apps:view" } },
    { path: "/console/migration-governance", name: "console-migration-governance", component: MigrationGovernancePage, meta: { requiresAuth: true, title: "迁移治理", titleKey: "route.consoleMigrationGovernance", requiresPermission: "apps:view" } },
    { path: "/console/app-db-migrations", name: "console-app-db-migrations", component: AppDatabaseMigrationPage, meta: { requiresAuth: true, title: "应用数据库迁移", titleKey: "route.consoleAppDbMigrations", requiresPermission: "apps:view" } },
    { path: "/console/tools", name: "console-tools", component: ToolsAuthorizationPage, meta: { requiresAuth: true, title: "工具授权中心", titleKey: "route.consoleTools", requiresPermission: "system:admin" } },
    { path: "/apps/:appId", name: "app-workspace-root", redirect: to => `/apps/${to.params.appId}/dashboard`, meta: { requiresAuth: true, title: "应用工作台", titleKey: "route.appWorkspace", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/dashboard", name: "app-workspace-dashboard", component: AppDashboardPage, meta: { requiresAuth: true, title: "应用仪表盘", titleKey: "route.appDashboard", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/builder", name: "app-workspace-builder", component: AppBuilderPage, meta: { requiresAuth: true, title: "应用设计器", titleKey: "route.appBuilder", requiresPermission: "apps:update" } },
    { path: "/apps/:appId/settings", name: "app-workspace-settings", component: AppSettingsPage, meta: { requiresAuth: true, title: "应用设置", titleKey: "route.appSettings", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/pages", name: "app-workspace-pages", component: AppPagesPage, meta: { requiresAuth: true, title: "页面管理", titleKey: "route.appPages", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/forms", name: "app-workspace-forms", component: AppFormsPage, meta: { requiresAuth: true, title: "表单管理", titleKey: "route.forms", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/forms/:id/designer", name: "app-workspace-form-designer", component: FormDesignerPage, meta: { requiresAuth: true, title: "表单设计器", titleKey: "route.formDesigner", requiresPermission: "apps:update" } },
    { path: "/apps/:appId/flows", name: "app-workspace-flows", component: AppFlowsPage, meta: { requiresAuth: true, title: "流程管理", titleKey: "route.processManage", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/agents", name: "app-workspace-agents", component: AgentListPage, meta: { requiresAuth: true, title: "Agent 列表", titleKey: "route.aiAgentList", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/multi-agent", name: "app-workspace-multi-agent-list", component: MultiAgentOrchestrationListPage, meta: { requiresAuth: true, title: "多Agent编排", titleKey: "route.aiMultiAgentList", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/multi-agent/:id", name: "app-workspace-multi-agent-detail", component: MultiAgentOrchestrationDetailPage, meta: { requiresAuth: true, title: "多Agent编排详情", titleKey: "route.aiMultiAgentDetail", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/evaluations/datasets", name: "app-workspace-evaluation-datasets", component: AiTestSetsPage, meta: { requiresAuth: true, title: "评测数据集", titleKey: "route.aiTestSets", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/evaluations/tasks", name: "app-workspace-evaluation-tasks", component: EvaluationTaskPage, meta: { requiresAuth: true, title: "评测任务", titleKey: "route.aiEvaluationTasks", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/evaluations/reports/:taskId", name: "app-workspace-evaluation-report", component: EvaluationReportPage, meta: { requiresAuth: true, title: "评测报告", titleKey: "route.aiEvaluationReport", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/memories", name: "app-workspace-memories", component: UserMemorySettingsPage, meta: { requiresAuth: true, title: "记忆管理", titleKey: "route.aiMemorySettings", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/knowledge-bases", name: "app-workspace-knowledge-bases", component: KnowledgeBaseListPage, meta: { requiresAuth: true, title: "知识库列表", titleKey: "route.knowledgeBaseList", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/knowledge-bases/:id", name: "app-workspace-knowledge-base-detail", component: KnowledgeBaseDetailPage, meta: { requiresAuth: true, title: "知识库详情", titleKey: "route.knowledgeBaseDetail", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/knowledge-bases/:id/test", name: "app-workspace-knowledge-base-test", component: KnowledgeBaseTestPage, meta: { requiresAuth: true, title: "检索测试", titleKey: "route.knowledgeBaseTest", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/workflows", name: "app-workspace-workflows", component: WorkflowListPage, meta: { requiresAuth: true, title: "工作流列表", titleKey: "route.aiWorkflowList", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/prompts", name: "app-workspace-prompts", component: AiPromptLibraryPage, meta: { requiresAuth: true, title: "Prompt 模板", titleKey: "route.aiPromptTemplates", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/model-configs", name: "app-workspace-model-configs", component: ModelConfigsPage, meta: { requiresAuth: true, title: "模型配置", titleKey: "route.modelConfigs", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/plugins", name: "app-workspace-plugins", component: AiPluginListPage, meta: { requiresAuth: true, title: "插件配置", titleKey: "route.aiPluginConfig", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/plugins/:id", name: "app-workspace-plugin-detail", component: AiPluginDetailPage, meta: { requiresAuth: true, title: "插件详情", titleKey: "route.aiPluginDetail", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/plugins/:id/apis/:apiId", name: "app-workspace-plugin-api-editor", component: AiPluginApiEditorPage, meta: { requiresAuth: true, title: "插件 API 编辑器", titleKey: "route.aiPluginApiEditor", requiresPermission: "apps:update" } },
    { path: "/apps/:appId/workflows/:id/editor", name: "app-workspace-workflow-editor", component: WorkflowEditorPage, meta: { requiresAuth: true, title: "工作流设计器", titleKey: "route.workflowEditor" } },
    { path: "/apps/:appId/agents/:id/edit", name: "app-workspace-agent-editor", component: AgentEditorPage, meta: { requiresAuth: true, title: "Agent 编辑", titleKey: "route.aiAgentEdit" } },
    { path: "/apps/:appId/data", name: "app-workspace-data", component: AppDataPage, meta: { requiresAuth: true, title: "数据管理", titleKey: "route.dataManage", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/data/erd", name: "app-workspace-data-erd", component: ERDCanvasPage, meta: { requiresAuth: true, title: "ERD 设计器", titleKey: "route.dataManage", requiresPermission: "apps:update" } },
    { path: "/apps/:appId/data/:tableKey", name: "app-workspace-data-crud", component: DynamicTableCrudPage, meta: { requiresAuth: true, title: "动态数据管理", titleKey: "route.dataManage", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/data/:tableKey/native", name: "app-workspace-data-native", component: DynamicRecordsNativePage, meta: { requiresAuth: true, title: "原生记录视图", titleKey: "route.dataManage", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/org", name: "app-workspace-org", component: AppOrganizationPage, meta: { requiresAuth: true, title: "组织管理", titleKey: "route.appOrganization", requiresPermission: "apps:view" } },
    { path: "/apps/:appId/permissions", name: "app-workspace-permissions", component: AppPermissionsPage, meta: { requiresAuth: true, title: "权限入口", titleKey: "route.permissionsEntry", requiresPermission: "apps:view" } },
    { path: "/r/:appKey/:pageKey", name: "runtime-delivery-page", component: PageRuntimeRenderer, meta: { requiresAuth: true, title: "运行交付面", titleKey: "route.runtimeDelivery" } },
    { path: "/process/instances/:id", name: "process-instance-detail", component: ApprovalInstanceDetailPage, meta: { requiresAuth: true, title: "流程详情", titleKey: "route.processDetail", requiresPermission: "approval:flow:view" } },
    { path: "/system/notifications", name: "system-notifications", component: NotificationsPage, meta: { requiresAuth: true, title: "通知中心", titleKey: "route.notifications" } },
    { path: "/system/notifications/manage", name: "system-notifications-manage", component: NotificationManagePage, meta: { requiresAuth: true, title: "公告管理", titleKey: "route.notificationsManage", requiresPermission: "notification:view" } },
    {
      path: "/inbox",
      name: "system-notifications-inbox-alias",
      redirect: to => ({ path: "/system/notifications", query: to.query, hash: to.hash }),
      meta: { requiresAuth: true, title: "通知中心", titleKey: "route.notifications" }
    },
    { path: "/settings/system/dict-types", name: "settings-system-dict-types", component: DictTypesPage, meta: { requiresAuth: true, title: "字典管理", titleKey: "route.dictTypes", requiresPermission: "dict:type:view" } },
    { path: "/settings/system/datasources", name: "settings-system-datasources", component: TenantDataSourcesPage, meta: { requiresAuth: true, title: "数据源管理", titleKey: "route.datasources", requiresPermission: "system:admin" } },
    { path: "/settings/system/configs", name: "settings-system-configs", component: SystemConfigsPage, meta: { requiresAuth: true, title: "参数配置", titleKey: "route.systemConfigs", requiresPermission: "config:view" } },
    { path: "/admin/ai-config", name: "admin-ai-config-static", component: AiConfigPage, meta: { requiresAuth: true, title: "AI 管理配置", requiresPermission: "ai-admin-config:view" } },
    { path: "/settings/ai/model-configs", name: "settings-ai-model-configs", component: ModelConfigsPage, meta: { requiresAuth: true, title: "模型配置", titleKey: "route.modelConfigs" } },
    { path: "/ai/variables", name: "ai-variables-static", component: AiVariablesPage, meta: { requiresAuth: true, title: "变量管理", titleKey: "route.aiVariables" } },
    { path: "/ai/open-platform", name: "ai-open-platform-static", component: AiOpenPlatformPage, meta: { requiresAuth: true, title: "开放平台", titleKey: "route.aiOpenPlatform" } },
    { path: "/ai/workspace", name: "ai-workspace-static", component: AiWorkspacePage, meta: { requiresAuth: true, title: "AI 工作台", titleKey: "route.aiWorkspace" } },
    { path: "/ai/library", name: "ai-library-static", component: AiLibraryPage, meta: { requiresAuth: true, title: "资源库", titleKey: "route.aiLibrary" } },
    { path: "/ai/devops/test-sets", name: "ai-test-sets-static", component: AiTestSetsPage, meta: { requiresAuth: true, title: "测试集", titleKey: "route.aiTestSets" } },
    { path: "/ai/devops/evaluations/tasks", name: "ai-evaluation-tasks-static", component: EvaluationTaskPage, meta: { requiresAuth: true, title: "评测任务", titleKey: "route.aiEvaluationTasks" } },
    { path: "/ai/devops/evaluations/reports/:taskId", name: "ai-evaluation-report-static", component: EvaluationReportPage, meta: { requiresAuth: true, title: "评测报告", titleKey: "route.aiEvaluationReport" } },
    { path: "/ai/devops/mock-sets", name: "ai-mock-sets-static", component: AiMockSetsPage, meta: { requiresAuth: true, title: "Mock 集", titleKey: "route.aiMockSets" } },
    { path: "/ai/shortcuts", name: "ai-shortcuts-static", component: AiShortcutsPage, meta: { requiresAuth: true, title: "快捷命令", titleKey: "route.aiShortcuts" } },
    { path: "/ai/search", name: "ai-search-static", component: AiSearchResultsPage, meta: { requiresAuth: true, title: "统一搜索", titleKey: "route.aiSearch" } },
    { path: "/ai/marketplace", name: "ai-marketplace-static", component: AiMarketplacePage, meta: { requiresAuth: true, title: "应用市场", titleKey: "route.aiMarketplace" } },
    { path: "/ai/multi-agent", name: "ai-multi-agent-static", component: MultiAgentOrchestrationListPage, meta: { requiresAuth: true, title: "多Agent编排", titleKey: "route.aiMultiAgentList" } },
    { path: "/ai/multi-agent/:id", name: "ai-multi-agent-detail-static", component: MultiAgentOrchestrationDetailPage, meta: { requiresAuth: true, title: "多Agent编排详情", titleKey: "route.aiMultiAgentDetail" } },
    { path: "/ai/memories", name: "ai-memories-static", component: UserMemorySettingsPage, meta: { requiresAuth: true, title: "记忆管理", titleKey: "route.aiMemorySettings" } },
    { path: "/ai/knowledge-bases", name: "ai-knowledge-bases-static", component: KnowledgeBaseListPage, meta: { requiresAuth: true, title: "知识库列表", titleKey: "route.knowledgeBaseList" } },
    { path: "/ai/knowledge-bases/:id", name: "ai-knowledge-base-detail-static", component: KnowledgeBaseDetailPage, meta: { requiresAuth: true, title: "知识库详情", titleKey: "route.knowledgeBaseDetail" } },
    { path: "/ai/knowledge-bases/:id/test", name: "ai-knowledge-base-test-static", component: KnowledgeBaseTestPage, meta: { requiresAuth: true, title: "检索测试", titleKey: "route.knowledgeBaseTest" } },
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
    { path: "/lowcode/apps", name: "app-list", component: AppListPage, meta: { requiresAuth: true, title: "低代码应用", titleKey: "route.lowcodeApps", requiresPermission: "apps:view" } },
    { path: "/lowcode/prototype", name: "custom-designer-prototype", component: CustomDesignerMockPage, meta: { requiresAuth: true, title: "自定义设计器 MVP", titleKey: "route.customDesignerMVP" } },
    { path: "/lowcode/forms", name: "apps-form-list", component: FormListPage, meta: { requiresAuth: true, title: "表单管理", titleKey: "route.forms", requiresPermission: "apps:view" } },
    { path: "/lowcode/templates", name: "template-market", component: TemplateMarketPage, meta: { requiresAuth: true, title: "模板市场", titleKey: "route.templateMarket", requiresPermission: "apps:view" } },
    { path: "/monitor/writeback-failures", name: "monitor-writeback-failures", component: WritebackMonitorPage, meta: { requiresAuth: true, title: "回写监控", titleKey: "route.writebackMonitor", requiresPermission: "system:admin" } },
    { path: "/approval/designer", name: "approval-designer", component: ApprovalDesignerPage, meta: { requiresAuth: true, title: "流程设计器", titleKey: "route.approvalDesigner", requiresPermission: "approval:flow:create" } },
    { path: "/approval/designer/:id", name: "approval-designer-edit", component: ApprovalDesignerPage, meta: { requiresAuth: true, title: "流程设计器", titleKey: "route.approvalDesigner", requiresPermission: "approval:flow:update" } },
    { path: "/approval/flows/manage", name: "approval-flows-manage", component: ApprovalFlowManagePage, meta: { requiresAuth: true, title: "流程发布总览", titleKey: "route.approvalFlowManage", requiresPermission: "approval:flow:manage" } },
    { path: "/approval/flows", name: "approval-flows", component: ApprovalFlowsPage, meta: { requiresAuth: true, title: "流程定义列表", titleKey: "route.approvalFlows", requiresPermission: "approval:flow:view" } },
    { path: "/approval/instances/manage", name: "approval-instances-manage", component: ApprovalInstanceManagePage, meta: { requiresAuth: true, title: "所有审批实例", titleKey: "route.approvalInstancesManage", requiresPermission: "system:admin" } },
    { path: "/approval/workspace", name: "approval-workspace", component: ApprovalWorkspacePage, meta: { requiresAuth: true, title: "审批工作台", titleKey: "route.approvalWorkspace" } },
    { path: "/settings/org/tenants", name: "settings-org-tenants", component: TenantsPage, meta: { requiresAuth: true, title: "租户管理", titleKey: "route.tenants", requiresPermission: "system:tenant:query" } },
    { path: "/settings/org/departments", name: "settings-org-deps", component: DepartmentsPage, meta: { requiresAuth: true, title: "组织架构", titleKey: "route.departments", requiresPermission: "departments:view" } },
    { path: "/settings/org/positions", name: "settings-org-positions", component: PositionsPage, meta: { requiresAuth: true, title: "职位名称", titleKey: "route.positions", requiresPermission: "positions:view" } },
    { path: "/settings/org/users", name: "settings-org-users", component: UsersPage, meta: { requiresAuth: true, title: "员工管理", titleKey: "route.users", requiresPermission: "users:view" } },
    { path: "/settings/auth/menus", name: "settings-auth-menus", component: MenusPage, meta: { requiresAuth: true, title: "菜单管理", titleKey: "route.menus", requiresPermission: "menus:view" } },
    { path: "/settings/projects", name: "settings-projects", component: ProjectsPage, meta: { requiresAuth: true, title: "项目管理", titleKey: "route.projects", requiresPermission: "projects:view" } },
    { path: "/system/file-transfer-demo", name: "system-file-transfer-demo", component: FileTransferDemoPage, meta: { requiresAuth: true, title: "文件传输演示", titleKey: "route.fileTransferDemo", requiresPermission: "file:upload" } },
    { path: "/assets", name: "assets-manage", component: AssetsPage, meta: { requiresAuth: true, title: "资产管理", titleKey: "route.assets", requiresPermission: "assets:view" } },
    { path: "/audit", name: "audit-manage", component: AuditPage, meta: { requiresAuth: true, title: "审计日志", titleKey: "route.audit", requiresPermission: "audit:view" } },
    { path: "/alert", name: "alert-manage", component: AlertPage, meta: { requiresAuth: true, title: "告警管理", titleKey: "route.alert", requiresPermission: "alert:view" } },
    // ==========================================
    // 以下为已弃用（Deprecated）路由，弃用窗口截止 2026-12-31
    // 请在弃用窗口结束前将所有调用方迁移至新路径
    // 弃用窗口内：仍可访问，但会执行重定向；不再新增功能
    // ==========================================
    {
      path: "/:pathMatch(.*)*",
      name: "not-found",
      component: NotFoundPage,
      meta: { title: "页面未找到", titleKey: "route.notFound" }
    }
  ]
});

const whiteList = ["/login", "/register"];

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

function consumeRuntimeMigrationBlock(
  appKey: string
): { blocked: boolean; message?: string } {
  if (typeof sessionStorage === "undefined") {
    return { blocked: false };
  }
  const raw = sessionStorage.getItem("atlas_runtime_migration_block");
  if (!raw) {
    return { blocked: false };
  }

  try {
    const payload = JSON.parse(raw) as { appKey?: string; expiresAt?: number; message?: string };
    const now = Date.now();
    if (!payload.appKey || !payload.expiresAt || payload.expiresAt <= now) {
      sessionStorage.removeItem("atlas_runtime_migration_block");
      return { blocked: false };
    }
    if (payload.appKey !== appKey) {
      return { blocked: false };
    }
    return { blocked: true, message: payload.message };
  } catch {
    sessionStorage.removeItem("atlas_runtime_migration_block");
    return { blocked: false };
  }
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
        await Promise.all([
          userStore.getInfo(),
          permissionStore.generateRoutes()
        ]);
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
        const requestError = err as ApiRequestErrorLike;
        const alreadyHandledByApiCore = typeof requestError?.status === "number" || requestError?.payload !== undefined;
        if (isAuthTerminalErrorLike(err)) {
          await userStore.logout({ skipRemote: true });
          if (!alreadyHandledByApiCore) {
            message.error(requestError?.message || translate("routerGuard.sessionReloadLogin"));
          }
          next({ path: "/login" });
          NProgress.done();
          return;
        }

        if (isNetworkErrorLike(err)) {
          // 网络异常时不清空登录态，保持当前页面可恢复。
          userStore.hydrateFromStorage();
          try {
            if (!permissionStore.routeLoaded) {
              await permissionStore.generateRoutes();
              permissionStore.registerRoutes(router);
            }
          } catch (networkRecoveryError) {
            console.warn("[router] 网络异常兜底恢复失败，等待在线后重试", networkRecoveryError);
          }
          next(false);
          NProgress.done();
          return;
        }

        if (!alreadyHandledByApiCore) {
          // 仅兜底提示非 API 异常，避免与 api-core 全局错误提示重复弹窗。
          message.error(requestError?.message || translate("routerGuard.sessionReloadLogin"));
        }
        next(false);
        NProgress.done();
        return;
      }
    }

    if (to.name === "runtime-delivery-page") {
      const runtimeAppKey = typeof to.params.appKey === "string" ? to.params.appKey.trim() : "";
      if (runtimeAppKey) {
        const blockedState = consumeRuntimeMigrationBlock(runtimeAppKey);
        if (blockedState.blocked) {
          message.warning(blockedState.message || translate("apiCore.appMigrationPending"));
          next({
            path: "/console/app-db-migrations",
            query: { appKey: runtimeAppKey, reason: "migration_pending" },
            replace: true
          });
          NProgress.done();
          return;
        }
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

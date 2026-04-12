import { useEffect, useMemo, type ReactElement } from "react";
import { BrowserRouter, Navigate, Outlet, Route, Routes, useLocation, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { Spin } from "@douyinfe/semi-ui";
import { IconGlobe, IconGridView1, IconUserGroup } from "@douyinfe/semi-icons";
import { CozeShell } from "@atlas/coze-shell-react";
import { LibraryPage, KnowledgeDetailPage, KnowledgeUploadPage, type LibraryKnowledgeApi } from "@atlas/library-module-react";
import {
  ApprovalAdminPage,
  DashboardsAdminPage,
  DepartmentsAdminPage,
  ProfileAdminPage,
  ReportsAdminPage,
  RolesAdminPage,
  SettingsAdminPage,
  UsersAdminPage,
  VisualizationAdminPage,
  PositionsAdminPage,
  type AdminModuleApi
} from "@atlas/module-admin-react";
import { ExplorePluginsPage, ExploreSearchPage, ExploreTemplatesPage, type ExploreModuleApi } from "@atlas/module-explore-react";
import { AgentChatPage, AiAssistantPage, BotIdePage, DevelopPage, ModelConfigsPage, type StudioModuleApi } from "@atlas/module-studio-react";
import { WorkflowEditorPage, WorkflowListPage, type WorkflowModuleApi } from "@atlas/module-workflow-react";
import { AuthProvider, useAuth } from "./auth-context";
import { BootstrapProvider, useBootstrap } from "./bootstrap-context";
import { AppI18nProvider, useAppI18n } from "./i18n";
import { APP_PERMISSIONS } from "@/constants/permissions";
import {
  getConfiguredAppKey,
  getAppRuntimeMode,
  rememberConfiguredAppKey,
  setUnauthorizedHandler
} from "@/services/api-core";
import { getLibraryPaged } from "@/services/api-ai-workspace";
import {
  createChunk,
  createKnowledgeBase,
  createKnowledgeDocumentByFile,
  deleteChunk,
  deleteKnowledgeBase,
  deleteKnowledgeDocument,
  getDocumentChunksPaged,
  getDocumentProgress,
  getKnowledgeBaseById,
  getKnowledgeBasesPaged,
  getKnowledgeDocumentsPaged,
  resegmentDocument,
  testKnowledgeRetrieval,
  updateChunk,
  updateKnowledgeBase
} from "@/services/api-knowledge";
import { HomePage } from "./pages/home-page";
import { LoginPage } from "./pages/login-page";
import { ForbiddenPage } from "./pages/placeholder-page";
import { AppSetupPage, PlatformNotReadyPage } from "./pages/status-page";
import {
  backupNow,
  getDatabaseInfo,
  listBackups,
  testConnection
} from "@/services/api-db-maintenance";
import {
  getMyCopyRecordsPaged,
  getMyInstancesPaged,
  getMyTasksPaged
} from "@/services/api-approval";
import {
  createDashboard,
  createReport,
  deleteDashboard,
  deleteReport,
  getDashboardsPaged,
  getReportsPaged,
  updateDashboard,
  updateReport
} from "@/services/api-reports";
import { getVisualizationInstances } from "@/services/api-visualization";
import {
  getProfile,
  getRolesPaged as getAppRolesPaged,
  getRoleDetail,
  getUserDetail,
  getUsersPaged,
  createUser,
  updateUser,
  deleteUser,
  createRole,
  updateRole,
  deleteRole,
  getDepartmentsAll,
  getDepartmentsPaged,
  createDepartment,
  updateDepartment,
  deleteDepartment,
  getPositionsPaged,
  getPositionsAll,
  getPositionDetail,
  createPosition,
  updatePosition,
  deletePosition,
  savePassword,
  saveProfile
} from "@/services/api-admin";
import {
  getAiPluginBuiltInMetadata,
  getAiPluginsPaged,
  getTemplatesPaged,
  getRecentAiEdits,
  searchAi
} from "@/services/api-explore";
import {
  createAgent,
  getAgentById,
  getAgentsPaged,
  updateAgent
} from "@/services/api-agent";
import { generateByAiAssistant } from "@/services/api-ai-assistant";
import { getModelConfigsPaged } from "@/services/api-model-config";
import {
  getConversationsPaged,
  getMessages
} from "@/services/api-conversation";
import {
  createWorkflow,
  listWorkflows,
  workflowV2Api
} from "@/services/api-workflow";

const libraryApi: LibraryKnowledgeApi = {
  listLibrary: getLibraryPaged,
  listKnowledgeBases: getKnowledgeBasesPaged,
  getKnowledgeBase: getKnowledgeBaseById,
  createKnowledgeBase,
  updateKnowledgeBase,
  deleteKnowledgeBase,
  listDocuments: getKnowledgeDocumentsPaged,
  uploadDocument: createKnowledgeDocumentByFile,
  deleteDocument: deleteKnowledgeDocument,
  getDocumentProgress,
  resegmentDocument,
  listChunks: getDocumentChunksPaged,
  createChunk,
  updateChunk,
  deleteChunk,
  runRetrievalTest: testKnowledgeRetrieval
};

function LoadingPage() {
  return (
    <div className="atlas-loading-page">
      <Spin size="large" />
    </div>
  );
}

function UnauthorizedNavigationBridge() {
  const navigate = useNavigate();
  const { appKey } = useParams();

  useEffect(() => {
    setUnauthorizedHandler(() => {
      navigate(`/apps/${encodeURIComponent(appKey || getConfiguredAppKey())}/login`, { replace: true });
    });

    return () => {
      setUnauthorizedHandler(null);
    };
  }, [appKey, navigate]);

  return null;
}

function ProtectedPage({ permission, children }: { permission?: string; children: ReactElement }) {
  const auth = useAuth();
  const { appKey = "" } = useParams();
  if (permission && !auth.hasPermission(permission)) {
    return <Navigate to={`/apps/${encodeURIComponent(appKey)}/forbidden`} replace />;
  }
  return children;
}

function createAdminApi(appKey: string): AdminModuleApi {
  return {
    listUsers: getUsersPaged,
    getUserDetail,
    createUser,
    updateUser,
    deleteUser,
    listRoles: getAppRolesPaged,
    getRoleDetail,
    createRole,
    updateRole,
    deleteRole,
    listDepartments: getDepartmentsPaged,
    listDepartmentsAll: getDepartmentsAll,
    createDepartment,
    updateDepartment,
    deleteDepartment,
    listPositions: getPositionsPaged,
    listPositionsAll: getPositionsAll,
    getPositionDetail,
    createPosition,
    updatePosition,
    deletePosition,
    listPendingApprovals: request => getMyTasksPaged(request),
    listDoneApprovals: request => getMyTasksPaged(request, 2),
    listMyRequests: request => getMyInstancesPaged(request),
    listCopyApprovals: request => getMyCopyRecordsPaged(request),
    listReports: request => getReportsPaged(appKey, request),
    createReport: request => createReport(appKey, request),
    updateReport: (id, request) => updateReport(appKey, id, request),
    deleteReport: id => deleteReport(appKey, id),
    listDashboards: request => getDashboardsPaged(appKey, request),
    createDashboard: request => createDashboard(appKey, request),
    updateDashboard: (id, request) => updateDashboard(appKey, id, request),
    deleteDashboard: id => deleteDashboard(appKey, id),
    listVisualization: request => getVisualizationInstances(appKey, request),
    getProfile,
    updateProfile: saveProfile,
    changePassword: savePassword,
    testConnection: () => testConnection().then(response => response.data ?? { connected: false, message: "Unavailable", latencyMs: null }),
    getDatabaseInfo: () => getDatabaseInfo().then(response => response.data ?? { dbType: "-", connectionString: "-", fileSizeBytes: null, journalMode: null, pageCount: null, pageSize: null }),
    listBackups: () => listBackups().then(response => response.data ?? []),
    backupNow: () => backupNow().then(response => response.data ?? { success: false, fileName: null, message: response.message ?? null, sizeBytes: null })
  };
}

function createExploreApi(): ExploreModuleApi {
  return {
    listPlugins: getAiPluginsPaged,
    listBuiltInPlugins: getAiPluginBuiltInMetadata,
    listTemplates: getTemplatesPaged,
    search: searchAi,
    recent: getRecentAiEdits
  };
}

function createStudioApi(appKey: string): StudioModuleApi {
  return {
    listAgents: getAgentsPaged,
    getAgent: getAgentById,
    createAgent,
    updateAgent,
    listConversations: agentId => getConversationsPaged(appKey, { pageIndex: 1, pageSize: 20 }, agentId),
    getMessages: conversationId => getMessages(appKey, conversationId),
    generateAssistant: (kind, description) => generateByAiAssistant(appKey, kind, description),
    listModelConfigs: () => getModelConfigsPaged({ pageIndex: 1, pageSize: 20 })
  };
}

function createWorkflowModuleApi(): WorkflowModuleApi {
  return {
    listWorkflows: () => listWorkflows(1, 20).then(response => response.data ?? { items: [], total: 0, pageIndex: 1, pageSize: 20 }),
    createWorkflow: () => createWorkflow({ name: `Workflow ${Date.now()}`, mode: 0 }).then(response => response.data ?? ""),
    apiClient: workflowV2Api
  };
}

function useAppApis(appKey: string) {
  return useMemo(() => ({
    adminApi: createAdminApi(appKey),
    exploreApi: createExploreApi(),
    studioApi: createStudioApi(appKey),
    workflowApi: createWorkflowModuleApi()
  }), [appKey]);
}

function getPrimaryKey(pathname: string): "workspace" | "explore" | "admin" {
  if (pathname.includes("/explore/") || pathname.includes("/search/")) {
    return "explore";
  }
  if (pathname.includes("/admin/") || /\/apps\/[^/]+\/(users|roles|departments|positions|approval|reports|dashboards|visualization|settings|profile)/.test(pathname)) {
    return "admin";
  }
  return "workspace";
}

function DefaultWorkspaceRedirect() {
  const { appKey = "" } = useParams();
  const bootstrap = useBootstrap();
  return <Navigate to={`/apps/${encodeURIComponent(appKey)}/space/${encodeURIComponent(bootstrap.spaceId)}/develop`} replace />;
}

function LegacyRedirect({ to }: { to: (appKey: string, spaceId: string, locationSearch: string) => string }) {
  const { appKey = "" } = useParams();
  const bootstrap = useBootstrap();
  const location = useLocation();
  return <Navigate to={to(appKey, bootstrap.spaceId, location.search)} replace />;
}

function LegacyKnowledgeRedirect() {
  const { appKey = "", id = "" } = useParams();
  const bootstrap = useBootstrap();
  const location = useLocation();
  return <Navigate to={`/apps/${encodeURIComponent(appKey)}/space/${encodeURIComponent(bootstrap.spaceId)}/knowledge/${encodeURIComponent(id)}${location.search}`} replace />;
}

function LegacyKnowledgeUploadRedirect() {
  const { appKey = "", id = "" } = useParams();
  const bootstrap = useBootstrap();
  const location = useLocation();
  return <Navigate to={`/apps/${encodeURIComponent(appKey)}/space/${encodeURIComponent(bootstrap.spaceId)}/knowledge/${encodeURIComponent(id)}/upload${location.search}`} replace />;
}

function LegacyWorkflowEditorRedirect() {
  const { appKey = "", id = "" } = useParams();
  return <Navigate to={`/apps/${encodeURIComponent(appKey)}/work_flow/${encodeURIComponent(id)}/editor`} replace />;
}

function AppShellRoute() {
  const { appKey = "" } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  const bootstrap = useBootstrap();
  const auth = useAuth();
  const { locale, setLocale } = useAppI18n();

  useEffect(() => {
    rememberConfiguredAppKey(appKey);
  }, [appKey]);

  useEffect(() => {
    if (auth.isAuthenticated && !auth.profile && !auth.loading) {
      void auth.ensureProfile();
    }
  }, [auth]);

  if (bootstrap.loading || auth.loading) {
    return <LoadingPage />;
  }

  if (!bootstrap.platformReady) {
    return <Navigate to="/platform-not-ready" replace />;
  }

  if (!bootstrap.appReady) {
    return <Navigate to="/app-setup" replace />;
  }

  if (!auth.isAuthenticated) {
    return <Navigate to={`/apps/${encodeURIComponent(appKey)}/login?redirect=${encodeURIComponent(location.pathname + location.search)}`} replace />;
  }

  const primaryKey = getPrimaryKey(location.pathname);
  const primaryItems = [
    {
      key: "workspace",
      label: locale === "zh-CN" ? "工作空间" : "Workspace",
      icon: <IconGridView1 />,
      path: `/apps/${encodeURIComponent(appKey)}/space/${encodeURIComponent(bootstrap.spaceId)}/develop`
    },
    {
      key: "explore",
      label: locale === "zh-CN" ? "探索" : "Explore",
      icon: <IconGlobe />,
      path: `/apps/${encodeURIComponent(appKey)}/explore/plugin`
    },
    {
      key: "admin",
      label: locale === "zh-CN" ? "管理" : "Management",
      icon: <IconUserGroup />,
      path: `/apps/${encodeURIComponent(appKey)}/admin/users`
    }
  ];

  const secondarySections = primaryKey === "workspace"
    ? [
        {
          key: "workspace",
          title: locale === "zh-CN" ? "工作空间" : "Workspace",
          items: [
            { key: "develop", label: locale === "zh-CN" ? "开发台" : "Develop", path: `/apps/${encodeURIComponent(appKey)}/space/${encodeURIComponent(bootstrap.spaceId)}/develop`, testId: "app-sidebar-item-develop" },
            { key: "library", label: locale === "zh-CN" ? "资源库" : "Library", path: `/apps/${encodeURIComponent(appKey)}/space/${encodeURIComponent(bootstrap.spaceId)}/library`, testId: "app-sidebar-item-library" },
            { key: "chat", label: locale === "zh-CN" ? "Agent 对话" : "Agent Chat", path: `/apps/${encodeURIComponent(appKey)}/space/${encodeURIComponent(bootstrap.spaceId)}/chat`, testId: "app-sidebar-item-agent-chat" },
            { key: "assistant", label: locale === "zh-CN" ? "AI 助手" : "AI Assistant", path: `/apps/${encodeURIComponent(appKey)}/space/${encodeURIComponent(bootstrap.spaceId)}/assistant`, testId: "app-sidebar-item-ai-assistant" },
            { key: "model-configs", label: locale === "zh-CN" ? "模型配置" : "Model Configs", path: `/apps/${encodeURIComponent(appKey)}/space/${encodeURIComponent(bootstrap.spaceId)}/model-configs`, testId: "app-sidebar-item-model-configs" },
            { key: "workflow", label: locale === "zh-CN" ? "工作流" : "Workflow", path: `/apps/${encodeURIComponent(appKey)}/work_flow`, testId: "app-sidebar-item-workflows" }
          ]
        }
      ]
    : primaryKey === "explore"
      ? [
          {
            key: "explore",
            title: locale === "zh-CN" ? "探索" : "Explore",
            items: [
              { key: "plugin", label: locale === "zh-CN" ? "插件商店" : "Plugin Store", path: `/apps/${encodeURIComponent(appKey)}/explore/plugin`, testId: "app-sidebar-item-explore-plugins" },
              { key: "template", label: locale === "zh-CN" ? "模板商店" : "Template Store", path: `/apps/${encodeURIComponent(appKey)}/explore/template`, testId: "app-sidebar-item-explore-templates" }
            ]
          }
        ]
      : [
          {
            key: "admin",
            title: locale === "zh-CN" ? "管理" : "Management",
            items: [
              { key: "users", label: locale === "zh-CN" ? "用户管理" : "Users", path: `/apps/${encodeURIComponent(appKey)}/admin/users`, testId: "app-sidebar-item-users" },
              { key: "roles", label: locale === "zh-CN" ? "角色管理" : "Roles", path: `/apps/${encodeURIComponent(appKey)}/admin/roles`, testId: "app-sidebar-item-roles" },
              { key: "departments", label: locale === "zh-CN" ? "部门管理" : "Departments", path: `/apps/${encodeURIComponent(appKey)}/admin/departments`, testId: "app-sidebar-item-departments" },
              { key: "positions", label: locale === "zh-CN" ? "职位管理" : "Positions", path: `/apps/${encodeURIComponent(appKey)}/admin/positions`, testId: "app-sidebar-item-positions" },
              { key: "approval", label: locale === "zh-CN" ? "审批工作台" : "Approval", path: `/apps/${encodeURIComponent(appKey)}/admin/approval`, testId: "app-sidebar-item-approval" },
              { key: "reports", label: locale === "zh-CN" ? "报表管理" : "Reports", path: `/apps/${encodeURIComponent(appKey)}/admin/reports`, testId: "app-sidebar-item-reports" },
              { key: "dashboards", label: locale === "zh-CN" ? "仪表盘管理" : "Dashboards", path: `/apps/${encodeURIComponent(appKey)}/admin/dashboards`, testId: "app-sidebar-item-dashboards" },
              { key: "visualization", label: locale === "zh-CN" ? "运行监控" : "Visualization", path: `/apps/${encodeURIComponent(appKey)}/admin/visualization`, testId: "app-sidebar-item-visualization" },
              { key: "settings", label: locale === "zh-CN" ? "设置" : "Settings", path: `/apps/${encodeURIComponent(appKey)}/admin/settings`, testId: "app-sidebar-item-settings" },
              { key: "profile", label: locale === "zh-CN" ? "个人中心" : "Profile", path: `/apps/${encodeURIComponent(appKey)}/admin/profile`, testId: "app-sidebar-item-profile" }
            ]
          }
        ];

  const headerTitle = primaryKey === "workspace"
    ? (locale === "zh-CN" ? "工作空间" : "Workspace")
    : primaryKey === "explore"
      ? (locale === "zh-CN" ? "探索" : "Explore")
      : (locale === "zh-CN" ? "管理" : "Management");

  return (
    <>
      <UnauthorizedNavigationBridge />
      <CozeShell
        appKey={appKey}
        workspaceLabel={bootstrap.workspaceLabel || appKey}
        activePath={location.pathname}
        activePrimaryKey={primaryKey}
        primaryItems={primaryItems}
        secondarySections={secondarySections}
        headerTitle={headerTitle}
        headerSubtitle={getAppRuntimeMode() === "direct" ? (locale === "zh-CN" ? "直连模式" : "Direct Mode") : (locale === "zh-CN" ? "平台代理" : "Platform Mode")}
        localeLabel={locale === "zh-CN" ? "English" : "中文"}
        userName={auth.profile?.displayName || auth.profile?.username || "Atlas"}
        onNavigate={navigate}
        onToggleLocale={() => setLocale(locale === "zh-CN" ? "en-US" : "zh-CN")}
        onOpenProfile={() => navigate(`/apps/${encodeURIComponent(appKey)}/admin/profile`)}
        onLogout={() => {
          void auth.logout().then(() => navigate(`/apps/${encodeURIComponent(appKey)}/login`, { replace: true }));
        }}
      >
        <Outlet />
      </CozeShell>
    </>
  );
}

function LibraryRoute() {
  const { appKey = "" } = useParams();
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  return <LibraryPage api={libraryApi} locale={locale} appKey={appKey} onNavigate={navigate} />;
}

function KnowledgeDetailRoute() {
  const { appKey = "", id = "" } = useParams();
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  return <KnowledgeDetailPage api={libraryApi} locale={locale} appKey={appKey} knowledgeBaseId={Number(id)} onNavigate={navigate} />;
}

function KnowledgeUploadRoute() {
  const { appKey = "", id = "" } = useParams();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  return <KnowledgeUploadPage api={libraryApi} locale={locale} appKey={appKey} knowledgeBaseId={Number(id)} initialType={searchParams.get("type")} onNavigate={navigate} />;
}

function DevelopRoute() {
  const { appKey = "" } = useParams();
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  const bootstrap = useBootstrap();
  const { studioApi } = useAppApis(appKey);
  return <DevelopPage api={studioApi} locale={locale} onOpenBot={botId => navigate(`/apps/${encodeURIComponent(appKey)}/space/${encodeURIComponent(bootstrap.spaceId)}/bot/${encodeURIComponent(botId)}`)} />;
}

function BotIdeRoute() {
  const { appKey = "", botId = "" } = useParams();
  const { locale } = useAppI18n();
  const { studioApi } = useAppApis(appKey);
  return <BotIdePage api={studioApi} locale={locale} botId={botId} />;
}

function AgentChatRoute() {
  const { appKey = "" } = useParams();
  const { locale } = useAppI18n();
  const { studioApi } = useAppApis(appKey);
  return <AgentChatPage api={studioApi} locale={locale} />;
}

function AiAssistantRoute() {
  const { appKey = "" } = useParams();
  const { locale } = useAppI18n();
  const { studioApi } = useAppApis(appKey);
  return <AiAssistantPage api={studioApi} locale={locale} />;
}

function ModelConfigsRoute() {
  const { appKey = "" } = useParams();
  const { locale } = useAppI18n();
  const { studioApi } = useAppApis(appKey);
  return <ModelConfigsPage api={studioApi} locale={locale} />;
}

function WorkflowListRoute() {
  const { appKey = "" } = useParams();
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  const { workflowApi } = useAppApis(appKey);
  return <WorkflowListPage api={workflowApi} locale={locale} onOpenEditor={id => navigate(`/apps/${encodeURIComponent(appKey)}/work_flow/${encodeURIComponent(id)}/editor`)} />;
}

function WorkflowEditorRoute() {
  const { appKey = "", id = "" } = useParams();
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  const { workflowApi } = useAppApis(appKey);
  return <WorkflowEditorPage api={workflowApi} locale={locale} workflowId={id} onBack={() => navigate(`/apps/${encodeURIComponent(appKey)}/work_flow`)} />;
}

function AdminUsersRoute() {
  const { appKey = "" } = useParams();
  const { locale } = useAppI18n();
  const { adminApi } = useAppApis(appKey);
  return <UsersAdminPage api={adminApi} locale={locale} />;
}

function AdminRolesRoute() {
  const { appKey = "" } = useParams();
  const { locale } = useAppI18n();
  const { adminApi } = useAppApis(appKey);
  return <RolesAdminPage api={adminApi} locale={locale} />;
}

function AdminDepartmentsRoute() {
  const { appKey = "" } = useParams();
  const { locale } = useAppI18n();
  const { adminApi } = useAppApis(appKey);
  return <DepartmentsAdminPage api={adminApi} locale={locale} />;
}

function AdminPositionsRoute() {
  const { appKey = "" } = useParams();
  const { locale } = useAppI18n();
  const { adminApi } = useAppApis(appKey);
  return <PositionsAdminPage api={adminApi} locale={locale} />;
}

function AdminApprovalRoute() {
  const { appKey = "" } = useParams();
  const { locale } = useAppI18n();
  const { adminApi } = useAppApis(appKey);
  return <ApprovalAdminPage api={adminApi} locale={locale} />;
}

function AdminReportsRoute() {
  const { appKey = "" } = useParams();
  const { locale } = useAppI18n();
  const { adminApi } = useAppApis(appKey);
  return <ReportsAdminPage api={adminApi} locale={locale} />;
}

function AdminDashboardsRoute() {
  const { appKey = "" } = useParams();
  const { locale } = useAppI18n();
  const { adminApi } = useAppApis(appKey);
  return <DashboardsAdminPage api={adminApi} locale={locale} />;
}

function AdminVisualizationRoute() {
  const { appKey = "" } = useParams();
  const { locale } = useAppI18n();
  const { adminApi } = useAppApis(appKey);
  return <VisualizationAdminPage api={adminApi} locale={locale} />;
}

function AdminSettingsRoute() {
  const { appKey = "" } = useParams();
  const { locale } = useAppI18n();
  const { adminApi } = useAppApis(appKey);
  return <SettingsAdminPage api={adminApi} locale={locale} />;
}

function AdminProfileRoute() {
  const { appKey = "" } = useParams();
  const { locale } = useAppI18n();
  const { adminApi } = useAppApis(appKey);
  return <ProfileAdminPage api={adminApi} locale={locale} />;
}

function ExplorePluginsRoute() {
  const { appKey = "" } = useParams();
  const { locale } = useAppI18n();
  const { exploreApi } = useAppApis(appKey);
  return <ExplorePluginsPage api={exploreApi} locale={locale} />;
}

function ExploreTemplatesRoute() {
  const { appKey = "" } = useParams();
  const { locale } = useAppI18n();
  const { exploreApi } = useAppApis(appKey);
  return <ExploreTemplatesPage api={exploreApi} locale={locale} />;
}

function ExploreSearchRoute() {
  const { appKey = "", word = "" } = useParams();
  const { locale } = useAppI18n();
  const { exploreApi } = useAppApis(appKey);
  return <ExploreSearchPage api={exploreApi} locale={locale} keyword={decodeURIComponent(word)} />;
}

export function AppRouter() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/platform-not-ready" element={<PlatformNotReadyPage />} />
        <Route path="/app-setup" element={<AppSetupPage />} />
        <Route path="/apps/:appKey/login" element={<LoginPage />} />
        <Route path="/apps/:appKey" element={<AppShellRoute />}>
          <Route index element={<DefaultWorkspaceRedirect />} />
          <Route path="dashboard" element={<DefaultWorkspaceRedirect />} />
          <Route path="space/:spaceId/develop" element={<DevelopRoute />} />
          <Route path="space/:spaceId/chat" element={<AgentChatRoute />} />
          <Route path="space/:spaceId/assistant" element={<AiAssistantRoute />} />
          <Route path="space/:spaceId/model-configs" element={<ModelConfigsRoute />} />
          <Route path="space/:spaceId/bot/:botId" element={<BotIdeRoute />} />
          <Route path="space/:spaceId/library" element={<ProtectedPage permission={APP_PERMISSIONS.KNOWLEDGE_BASE_VIEW}><LibraryRoute /></ProtectedPage>} />
          <Route path="space/:spaceId/knowledge/:id" element={<ProtectedPage permission={APP_PERMISSIONS.KNOWLEDGE_BASE_VIEW}><KnowledgeDetailRoute /></ProtectedPage>} />
          <Route path="space/:spaceId/knowledge/:id/upload" element={<ProtectedPage permission={APP_PERMISSIONS.KNOWLEDGE_BASE_UPDATE}><KnowledgeUploadRoute /></ProtectedPage>} />
          <Route path="explore/plugin" element={<ExplorePluginsRoute />} />
          <Route path="explore/template" element={<ExploreTemplatesRoute />} />
          <Route path="search/:word" element={<ExploreSearchRoute />} />
          <Route path="work_flow" element={<WorkflowListRoute />} />
          <Route path="work_flow/:id/editor" element={<WorkflowEditorRoute />} />
          <Route path="admin/users" element={<AdminUsersRoute />} />
          <Route path="admin/roles" element={<AdminRolesRoute />} />
          <Route path="admin/departments" element={<AdminDepartmentsRoute />} />
          <Route path="admin/positions" element={<AdminPositionsRoute />} />
          <Route path="admin/approval" element={<AdminApprovalRoute />} />
          <Route path="admin/reports" element={<AdminReportsRoute />} />
          <Route path="admin/dashboards" element={<AdminDashboardsRoute />} />
          <Route path="admin/visualization" element={<AdminVisualizationRoute />} />
          <Route path="admin/settings" element={<AdminSettingsRoute />} />
          <Route path="admin/profile" element={<AdminProfileRoute />} />
          <Route path="library" element={<LegacyRedirect to={(nextAppKey, spaceId, search) => `/apps/${encodeURIComponent(nextAppKey)}/space/${encodeURIComponent(spaceId)}/library${search}`} />} />
          <Route path="knowledge-bases" element={<LegacyRedirect to={(nextAppKey, spaceId) => `/apps/${encodeURIComponent(nextAppKey)}/space/${encodeURIComponent(spaceId)}/library?type=knowledge-base&biz=library`} />} />
          <Route path="knowledge/:id" element={<LegacyKnowledgeRedirect />} />
          <Route path="knowledge/:id/upload" element={<LegacyKnowledgeUploadRedirect />} />
          <Route path="users" element={<Navigate to="admin/users" replace />} />
          <Route path="roles" element={<Navigate to="admin/roles" replace />} />
          <Route path="departments" element={<Navigate to="admin/departments" replace />} />
          <Route path="positions" element={<Navigate to="admin/positions" replace />} />
          <Route path="approval" element={<Navigate to="admin/approval" replace />} />
          <Route path="reports" element={<Navigate to="admin/reports" replace />} />
          <Route path="dashboards" element={<Navigate to="admin/dashboards" replace />} />
          <Route path="visualization" element={<Navigate to="admin/visualization" replace />} />
          <Route path="settings" element={<Navigate to="admin/settings" replace />} />
          <Route path="profile" element={<Navigate to="admin/profile" replace />} />
          <Route path="ai/agents" element={<LegacyRedirect to={(nextAppKey, spaceId) => `/apps/${encodeURIComponent(nextAppKey)}/space/${encodeURIComponent(spaceId)}/develop`} />} />
          <Route path="ai/chat/:agentId?" element={<LegacyRedirect to={(nextAppKey, spaceId) => `/apps/${encodeURIComponent(nextAppKey)}/space/${encodeURIComponent(spaceId)}/chat`} />} />
          <Route path="ai/assistant" element={<LegacyRedirect to={(nextAppKey, spaceId) => `/apps/${encodeURIComponent(nextAppKey)}/space/${encodeURIComponent(spaceId)}/assistant`} />} />
          <Route path="model-configs" element={<LegacyRedirect to={(nextAppKey, spaceId) => `/apps/${encodeURIComponent(nextAppKey)}/space/${encodeURIComponent(spaceId)}/model-configs`} />} />
          <Route path="workflows" element={<Navigate to="work_flow" replace />} />
          <Route path="workflows/:id/editor" element={<LegacyWorkflowEditorRedirect />} />
          <Route path="forbidden" element={<ForbiddenPage />} />
          <Route path="*" element={<DefaultWorkspaceRedirect />} />
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

export function AppRoot() {
  return (
    <AppI18nProvider>
      <BootstrapProvider>
        <AuthProvider>
          <AppRouter />
        </AuthProvider>
      </BootstrapProvider>
    </AppI18nProvider>
  );
}

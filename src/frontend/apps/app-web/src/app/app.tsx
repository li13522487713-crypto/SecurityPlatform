import { useEffect, useMemo, useState, type ReactElement } from "react";
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
import {
  AgentChatPage,
  AiAssistantPage,
  BotIdePage,
  DevelopPage,
  ModelConfigsPage,
  type DevelopFocus,
  type DevelopResourceSummary,
  type StudioModuleApi
} from "@atlas/module-studio-react";
import {
  WorkflowEditorPage,
  WorkflowListPage,
  type WorkflowCreateRequest as WorkflowModuleCreateRequest,
  type WorkflowListQuery,
  type WorkflowModuleApi,
  type WorkflowResourceMode,
  type WorkflowTemplateSummary
} from "@atlas/module-workflow-react";
import {
  adminPath,
  appForbiddenPath,
  appSignPath,
  explorePath,
  inferPrimaryArea,
  replacePathAppKey,
  workflowEditorPath,
  workflowListPath,
  workspaceAssistantPath,
  workspaceBotPath,
  workspaceChatPath,
  workspaceDevelopPath,
  workspaceLibraryPath,
  workspaceModelConfigsPath
} from "@atlas/app-shell-shared";
import { AuthProvider, useAuth } from "./auth-context";
import { BootstrapProvider, useBootstrap } from "./bootstrap-context";
import { AppI18nProvider, useAppI18n } from "./i18n";
import { APP_PERMISSIONS } from "@/constants/permissions";
import {
  getConfiguredAppKey,
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
import { EntryGatewayPage } from "./pages/entry-gateway-page";
import { ForbiddenPage } from "./pages/forbidden-page";
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
  bindAgentWorkflow,
  createAgent,
  getAgentById,
  getAgentsPaged,
  updateAgent
} from "@/services/api-agent";
import { generateByAiAssistant } from "@/services/api-ai-assistant";
import {
  createModelConfig,
  createModelConfigPromptTestStream,
  deleteModelConfig,
  getModelConfigById,
  getModelConfigStats,
  getModelConfigsPaged,
  testModelConfigConnection,
  updateModelConfig
} from "@/services/api-model-config";
import {
  appendConversationMessage,
  createAgentChatStream,
  createConversation,
  getConversationsPaged,
  getMessages
} from "@/services/api-conversation";
import {
  copyWorkflow,
  createWorkflow as createWorkflowDefinition,
  deleteWorkflow as deleteWorkflowDefinition,
  listWorkflowVersions,
  listWorkflows,
  workflowV2Api
} from "@/services/api-workflow";
import { executeWorkflowTask } from "@/services/api-workflow-playground";

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

const WORKFLOW_TEMPLATE_REGISTRY: WorkflowTemplateSummary[] = [
  {
    id: "workflow-blank",
    title: "空白工作流",
    description: "从空白画布开始构建审批、数据处理或多步骤自动化流程。",
    mode: "workflow",
    createSource: "blank",
    badge: "Blank"
  },
  {
    id: "workflow-approval",
    title: "审批流程模板",
    description: "适用于申请、审批、通知等标准业务流。",
    mode: "workflow",
    createSource: "template",
    badge: "Template"
  },
  {
    id: "workflow-knowledge",
    title: "知识检索模板",
    description: "适用于知识库召回、总结和结果输出的场景。",
    mode: "workflow",
    createSource: "template",
    badge: "Template"
  },
  {
    id: "chatflow-blank",
    title: "空白 Chatflow",
    description: "从空白对话流开始构建多轮交互与消息处理。",
    mode: "chatflow",
    createSource: "blank",
    badge: "Blank"
  },
  {
    id: "chatflow-assistant",
    title: "客服对话模板",
    description: "适用于客服问答、澄清提问和多轮引导。",
    mode: "chatflow",
    createSource: "template",
    badge: "Template"
  },
  {
    id: "chatflow-copilot",
    title: "应用 Copilot 模板",
    description: "适用于面向应用内用户的智能助理和任务引导。",
    mode: "chatflow",
    createSource: "template",
    badge: "Template"
  }
];

function toWorkflowModeValue(mode: WorkflowResourceMode): 0 | 1 {
  return mode === "chatflow" ? 1 : 0;
}

function toWorkflowResourceSummary(
  item: {
    id: string;
    name: string;
    description?: string;
    updatedAt?: string;
    latestVersionNumber?: number;
    status?: number;
  },
  kind: "workflow" | "chatflow"
): DevelopResourceSummary {
  return {
    id: item.id,
    kind,
    title: item.name,
    description: item.description,
    updatedAt: item.updatedAt,
    status: item.status === 1 ? "已发布" : "草稿",
    meta: `v${item.latestVersionNumber ?? 0}`
  };
}

function navGlyph(label: string) {
  return <span className="app-nav-glyph" aria-hidden="true">{label}</span>;
}

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
      navigate(appSignPath(appKey || getConfiguredAppKey()), { replace: true });
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
    return <Navigate to={appForbiddenPath(appKey)} replace />;
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

async function* createAgentMessageStream(
  appKey: string,
  agentId: string,
  request: { conversationId?: string; message: string; enableRag?: boolean }
): AsyncIterable<{ type: "chunk" | "final" | "thought"; content: string }> {
  const { fetchPromise } = createAgentChatStream(
    appKey,
    agentId,
    {
      conversationId: request.conversationId,
      message: request.message,
      enableRag: request.enableRag
    },
    "react"
  );
  const response = await fetchPromise;
  if (!response.ok || !response.body) {
    throw new Error("Agent chat stream failed");
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = "";

  while (true) {
    const { done, value } = await reader.read();
    if (done) {
      break;
    }

    buffer += decoder.decode(value, { stream: true });
    const frames = buffer.split("\n\n");
    buffer = frames.pop() ?? "";

    for (const frame of frames) {
      const lines = frame.split("\n");
      let eventType = "message";
      const dataLines: string[] = [];

      for (const line of lines) {
        if (line.startsWith("event:")) {
          eventType = line.slice(6).trim();
          continue;
        }

        if (line.startsWith("data:")) {
          dataLines.push(line.slice(5).trimStart());
        }
      }

      const payload = dataLines.join("\n").trim();
      if (!payload || eventType === "done" || payload === "[DONE]") {
        continue;
      }

      if (eventType === "thought") {
        yield { type: "thought", content: payload };
        continue;
      }

      if (eventType === "final") {
        yield { type: "final", content: payload };
        continue;
      }

      yield { type: "chunk", content: payload };
    }
  }
}

function createStudioApi(appKey: string): StudioModuleApi {
  return {
    listAgents: getAgentsPaged,
    getAgent: getAgentById,
    createAgent,
    updateAgent,
    listConversations: agentId => getConversationsPaged(appKey, { pageIndex: 1, pageSize: 20 }, agentId),
    getMessages: conversationId => getMessages(appKey, conversationId),
    createConversation: (agentId, title) => createConversation(appKey, agentId, title),
    sendAgentMessage: (agentId, request) => createAgentMessageStream(appKey, agentId, request),
    appendConversationMessage: (conversationId, request) => appendConversationMessage(appKey, conversationId, request),
    listWorkflows: async params => {
      const response = await listWorkflows(1, 100, params?.keyword);
      const status = params?.status ?? "all";
      return (response.data?.items ?? [])
        .filter(item => item.mode === 0)
        .filter(item => {
          if (status === "all") {
            return true;
          }

          return status === "published" ? item.status === 1 : item.status !== 1;
        })
        .map(item => ({
          id: item.id,
          name: item.name,
          description: item.description,
          status: item.status,
          latestVersionNumber: item.latestVersionNumber,
          updatedAt: item.updatedAt
        }));
    },
    bindAgentWorkflow: (agentId, workflowId) => bindAgentWorkflow(agentId, workflowId),
    runWorkflowTask: (workflowId, incident) => executeWorkflowTask(workflowId, {
      incident,
      source: "draft"
    }),
    generateAssistant: (kind, description) => generateByAiAssistant(appKey, kind, description),
    listModelConfigs: () => getModelConfigsPaged({ pageIndex: 1, pageSize: 50 }),
    getModelConfig: getModelConfigById,
    getModelConfigStats,
    createModelConfig,
    updateModelConfig,
    deleteModelConfig,
    testModelConfigConnection,
    runModelConfigPromptTest: async request => {
      const { fetchPromise } = createModelConfigPromptTestStream(request);
      const response = await fetchPromise;
      if (!response.ok || !response.body) {
        throw new Error("Prompt test failed");
      }

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = "";
      let currentEvent = "message";
      let text = "";

      while (true) {
        const { done, value } = await reader.read();
        if (done) {
          break;
        }

        buffer += decoder.decode(value, { stream: true });
        const frames = buffer.split("\n\n");
        buffer = frames.pop() ?? "";

        for (const frame of frames) {
          const lines = frame.split("\n");
          const dataLines: string[] = [];
          currentEvent = "message";

          for (const line of lines) {
            if (line.startsWith("event:")) {
              currentEvent = line.slice(6).trim();
            } else if (line.startsWith("data:")) {
              dataLines.push(line.slice(5).trimStart());
            }
          }

          const payload = dataLines.join("\n");
          if (!payload || currentEvent === "done" || payload === "[DONE]") {
            continue;
          }

          text += payload;
        }
      }

      return text.trim();
    }
  };
}

function createWorkflowModuleApi(): WorkflowModuleApi {
  return {
    listWorkflows: async (query?: WorkflowListQuery) => {
      const pageIndex = Math.max(1, query?.pageIndex ?? 1);
      const pageSize = Math.max(1, query?.pageSize ?? 20);
      const response = await listWorkflows(1, 200, query?.keyword);
      const allItems = response.data?.items ?? [];
      const filtered = allItems.filter((item) => {
        const itemMode: WorkflowResourceMode = item.mode === 1 ? "chatflow" : "workflow";
        const modeMatched = !query?.mode || itemMode === query.mode;
        const statusMatched =
          !query?.status ||
          query.status === "all" ||
          (query.status === "published" ? item.status === 1 : item.status !== 1);
        return modeMatched && statusMatched;
      });
      const start = (pageIndex - 1) * pageSize;
      const items = filtered.slice(start, start + pageSize);
      return {
        items,
        total: filtered.length,
        pageIndex,
        pageSize
      };
    },
    listTemplates: async (mode) => WORKFLOW_TEMPLATE_REGISTRY.filter((item) => item.mode === mode),
    createWorkflow: async (request: WorkflowModuleCreateRequest) => {
      const response = await createWorkflowDefinition({
        name: request.name,
        description: request.description,
        mode: toWorkflowModeValue(request.mode)
      });
      return response.data ?? "";
    },
    duplicateWorkflow: async (id: string) => {
      const response = await copyWorkflow(id);
      return response.data ?? "";
    },
    deleteWorkflow: async (id: string) => {
      await deleteWorkflowDefinition(id);
    },
    getVersions: async (id: string) => {
      const response = await listWorkflowVersions(id);
      return (response.data ?? []).map((item) => ({
        id: item.id,
        versionNumber: item.versionNumber,
        publishedAt: item.publishedAt
      }));
    },
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

function DefaultWorkspaceRedirect() {
  const { appKey = "" } = useParams();
  const bootstrap = useBootstrap();
  return <Navigate to={workspaceDevelopPath(appKey, bootstrap.spaceId)} replace />;
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

  if (bootstrap.appKey && bootstrap.appKey !== appKey) {
    return <Navigate to={`${replacePathAppKey(location.pathname, bootstrap.appKey)}${location.search}`} replace />;
  }

  if (!bootstrap.platformReady) {
    return <Navigate to="/platform-not-ready" replace />;
  }

  if (!bootstrap.appReady) {
    return <Navigate to="/app-setup" replace />;
  }

  if (!auth.isAuthenticated) {
    return <Navigate to={appSignPath(appKey, location.pathname + location.search)} replace />;
  }

  const primaryKey = inferPrimaryArea(location.pathname);
  const primaryItems = [
    {
      key: "workspace",
      label: locale === "zh-CN" ? "工作空间" : "Workspace",
      icon: <IconGridView1 />,
      path: workspaceDevelopPath(appKey, bootstrap.spaceId),
      testId: "app-primary-item-workspace"
    },
    {
      key: "explore",
      label: locale === "zh-CN" ? "探索" : "Explore",
      icon: <IconGlobe />,
      path: explorePath(appKey, "plugin"),
      testId: "app-primary-item-explore"
    },
    {
      key: "admin",
      label: locale === "zh-CN" ? "管理" : "Management",
      icon: <IconUserGroup />,
      path: adminPath(appKey, "users"),
      testId: "app-primary-item-admin"
    }
  ];

  const secondarySections = primaryKey === "workspace"
    ? [
        {
          key: "workspace",
          title: locale === "zh-CN" ? "工作空间" : "Workspace",
          items: [
            {
              key: "develop",
              label: locale === "zh-CN" ? "开发台" : "Develop",
              icon: navGlyph("D"),
              badge: "Core",
              path: workspaceDevelopPath(appKey, bootstrap.spaceId),
              testId: "app-sidebar-item-develop"
            },
            {
              key: "agents",
              label: locale === "zh-CN" ? "Agent" : "Agent",
              icon: navGlyph("A"),
              path: `${workspaceDevelopPath(appKey, bootstrap.spaceId)}?focus=agents`,
              testId: "app-sidebar-item-agents"
            },
            {
              key: "library",
              label: locale === "zh-CN" ? "资源库" : "Library",
              icon: navGlyph("L"),
              path: workspaceLibraryPath(appKey, bootstrap.spaceId),
              testId: "app-sidebar-item-library"
            },
            {
              key: "chat",
              label: locale === "zh-CN" ? "Agent 对话" : "Agent Chat",
              icon: navGlyph("C"),
              path: workspaceChatPath(appKey, bootstrap.spaceId),
              testId: "app-sidebar-item-agent-chat"
            },
            {
              key: "assistant",
              label: locale === "zh-CN" ? "AI 助手" : "AI Assistant",
              icon: navGlyph("AI"),
              path: workspaceAssistantPath(appKey, bootstrap.spaceId),
              testId: "app-sidebar-item-ai-assistant"
            },
            {
              key: "model-configs",
              label: locale === "zh-CN" ? "模型配置" : "Model Configs",
              icon: navGlyph("M"),
              path: workspaceModelConfigsPath(appKey, bootstrap.spaceId),
              testId: "app-sidebar-item-model-configs"
            },
            {
              key: "workflow",
              label: locale === "zh-CN" ? "工作流" : "Workflow",
              icon: navGlyph("W"),
              badge: "Flow",
              path: workflowListPath(appKey),
              testId: "app-sidebar-item-workflows"
            },
            {
              key: "chatflow",
              label: locale === "zh-CN" ? "Chatflow" : "Chatflow",
              icon: navGlyph("CF"),
              badge: "Flow",
              path: `/apps/${encodeURIComponent(appKey)}/chat_flow`,
              testId: "app-sidebar-item-chatflows"
            }
          ]
        }
      ]
    : primaryKey === "explore"
      ? [
          {
            key: "explore",
            title: locale === "zh-CN" ? "探索" : "Explore",
            items: [
              { key: "plugin", label: locale === "zh-CN" ? "插件商店" : "Plugin Store", path: explorePath(appKey, "plugin"), testId: "app-sidebar-item-explore-plugins" },
              { key: "template", label: locale === "zh-CN" ? "模板商店" : "Template Store", path: explorePath(appKey, "template"), testId: "app-sidebar-item-explore-templates" }
            ]
          }
        ]
      : [
          {
            key: "admin",
            title: locale === "zh-CN" ? "管理" : "Management",
            items: [
              { key: "users", label: locale === "zh-CN" ? "用户管理" : "Users", path: adminPath(appKey, "users"), testId: "app-sidebar-item-users" },
              { key: "roles", label: locale === "zh-CN" ? "角色管理" : "Roles", path: adminPath(appKey, "roles"), testId: "app-sidebar-item-roles" },
              { key: "departments", label: locale === "zh-CN" ? "部门管理" : "Departments", path: adminPath(appKey, "departments"), testId: "app-sidebar-item-departments" },
              { key: "positions", label: locale === "zh-CN" ? "职位管理" : "Positions", path: adminPath(appKey, "positions"), testId: "app-sidebar-item-positions" },
              { key: "approval", label: locale === "zh-CN" ? "审批工作台" : "Approval", path: adminPath(appKey, "approval"), testId: "app-sidebar-item-approval" },
              { key: "reports", label: locale === "zh-CN" ? "报表管理" : "Reports", path: adminPath(appKey, "reports"), testId: "app-sidebar-item-reports" },
              { key: "dashboards", label: locale === "zh-CN" ? "仪表盘管理" : "Dashboards", path: adminPath(appKey, "dashboards"), testId: "app-sidebar-item-dashboards" },
              { key: "visualization", label: locale === "zh-CN" ? "运行监控" : "Visualization", path: adminPath(appKey, "visualization"), testId: "app-sidebar-item-visualization" },
              { key: "settings", label: locale === "zh-CN" ? "设置" : "Settings", path: adminPath(appKey, "settings"), testId: "app-sidebar-item-settings" },
              { key: "profile", label: locale === "zh-CN" ? "个人中心" : "Profile", path: adminPath(appKey, "profile"), testId: "app-sidebar-item-profile" }
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
        headerSubtitle={locale === "zh-CN" ? "应用宿主" : "App Host"}
        localeLabel={locale === "zh-CN" ? "English" : "中文"}
        userName={auth.profile?.displayName || auth.profile?.username || "Atlas"}
        onNavigate={navigate}
        onToggleLocale={() => setLocale(locale === "zh-CN" ? "en-US" : "zh-CN")}
        onOpenProfile={() => navigate(adminPath(appKey, "profile"))}
        onLogout={() => {
          void auth.logout().then(() => navigate(appSignPath(appKey), { replace: true }));
        }}
      >
        <Outlet />
      </CozeShell>
    </>
  );
}

function LibraryRoute() {
  const { appKey = "", spaceId = "" } = useParams();
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  return <LibraryPage api={libraryApi} locale={locale} appKey={appKey} spaceId={spaceId} onNavigate={navigate} />;
}

function KnowledgeDetailRoute() {
  const { appKey = "", id = "", spaceId = "" } = useParams();
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  return <KnowledgeDetailPage api={libraryApi} locale={locale} appKey={appKey} spaceId={spaceId} knowledgeBaseId={Number(id)} onNavigate={navigate} />;
}

function KnowledgeUploadRoute() {
  const { appKey = "", id = "", spaceId = "" } = useParams();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  return <KnowledgeUploadPage api={libraryApi} locale={locale} appKey={appKey} spaceId={spaceId} knowledgeBaseId={Number(id)} initialType={searchParams.get("type")} onNavigate={navigate} />;
}

function DevelopRoute() {
  const { appKey = "" } = useParams();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { locale } = useAppI18n();
  const bootstrap = useBootstrap();
  const { studioApi, workflowApi } = useAppApis(appKey);
  const [workflowItems, setWorkflowItems] = useState<DevelopResourceSummary[]>([]);
  const [chatflowItems, setChatflowItems] = useState<DevelopResourceSummary[]>([]);

  const focus = (searchParams.get("focus") as DevelopFocus | null) ?? "overview";

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      const [workflowResult, chatflowResult] = await Promise.all([
        workflowApi.listWorkflows({ pageIndex: 1, pageSize: 8, mode: "workflow" }),
        workflowApi.listWorkflows({ pageIndex: 1, pageSize: 8, mode: "chatflow" })
      ]);

      if (cancelled) {
        return;
      }

      setWorkflowItems(workflowResult.items.map((item) => toWorkflowResourceSummary(item, "workflow")));
      setChatflowItems(chatflowResult.items.map((item) => toWorkflowResourceSummary(item, "chatflow")));
    };

    void load();
    return () => {
      cancelled = true;
    };
  }, [workflowApi]);

  return (
    <DevelopPage
      api={studioApi}
      locale={locale}
      focus={focus}
      workflowItems={workflowItems}
      chatflowItems={chatflowItems}
      onOpenBot={botId => navigate(workspaceBotPath(appKey, bootstrap.spaceId, botId))}
      onOpenWorkflow={workflowId => navigate(workflowEditorPath(appKey, workflowId))}
      onOpenChatflow={workflowId => navigate(`/apps/${encodeURIComponent(appKey)}/chat_flow/${encodeURIComponent(workflowId)}/editor`)}
      onOpenWorkflows={() => navigate(workflowListPath(appKey))}
      onOpenChatflows={() => navigate(`/apps/${encodeURIComponent(appKey)}/chat_flow`)}
      onOpenAgentChat={() => navigate(workspaceChatPath(appKey, bootstrap.spaceId))}
      onOpenModelConfigs={() => navigate(workspaceModelConfigsPath(appKey, bootstrap.spaceId))}
      onCreateWorkflow={() => navigate(`${workflowListPath(appKey)}?create=1`)}
      onCreateChatflow={() => navigate(`/apps/${encodeURIComponent(appKey)}/chat_flow?create=1`)}
    />
  );
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

function WorkflowListRoute({ mode = "workflow" }: { mode?: WorkflowResourceMode }) {
  const { appKey = "" } = useParams();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  const { workflowApi } = useAppApis(appKey);
  const isChatflow = mode === "chatflow";
  return (
    <WorkflowListPage
      api={workflowApi}
      locale={locale}
      mode={mode}
      initialCreateVisible={searchParams.get("create") === "1"}
      onOpenEditor={id =>
        navigate(
          isChatflow
            ? `/apps/${encodeURIComponent(appKey)}/chat_flow/${encodeURIComponent(id)}/editor`
            : workflowEditorPath(appKey, id)
        )
      }
    />
  );
}

function WorkflowEditorRoute({ mode = "workflow" }: { mode?: WorkflowResourceMode }) {
  const { appKey = "", id = "" } = useParams();
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  const { workflowApi } = useAppApis(appKey);
  const backPath = mode === "chatflow"
    ? `/apps/${encodeURIComponent(appKey)}/chat_flow`
    : workflowListPath(appKey);
  return (
    <WorkflowEditorPage
      api={workflowApi}
      locale={locale}
      workflowId={id}
      mode={mode}
      backPath={backPath}
      onBack={() => {
        navigate(backPath, { replace: true });
      }}
    />
  );
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
        <Route path="/apps/:appKey/sign" element={<LoginPage />} />
        <Route path="/apps/:appKey" element={<AppShellRoute />}>
          <Route index element={<DefaultWorkspaceRedirect />} />
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
          <Route path="entry" element={<EntryGatewayPage />} />
          <Route path="work_flow" element={<WorkflowListRoute mode="workflow" />} />
          <Route path="work_flow/:id/editor" element={<WorkflowEditorRoute mode="workflow" />} />
          <Route path="chat_flow" element={<WorkflowListRoute mode="chatflow" />} />
          <Route path="chat_flow/:id/editor" element={<WorkflowEditorRoute mode="chatflow" />} />
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

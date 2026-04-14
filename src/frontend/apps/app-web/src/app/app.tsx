import { useEffect, useMemo, useState, type ReactElement } from "react";
import { BrowserRouter, Navigate, Outlet, Route, Routes, useLocation, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { Spin } from "@douyinfe/semi-ui";
import { CozeShell, type CozeNavSection } from "@atlas/coze-shell-react";
import { LibraryPage, KnowledgeDetailPage, KnowledgeUploadPage, type LibraryKnowledgeApi } from "@atlas/library-module-react";
import {
  OrganizationOverviewPage,
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
import { ExplorePluginDetailPage, ExploreTemplateDetailPage } from "@atlas/module-explore-react";
import {
  AppsPage,
  AgentChatPage,
  AssistantsPage,
  AiAssistantPage,
  AppDetailPage,
  AppPublishPage,
  AssistantPublishPage,
  BotIdePage,
  DataResourcesPage,
  DashboardPage,
  DatabaseDetailPage,
  DevelopPage,
  DatabasesPage,
  KnowledgeBasesPage,
  ModelConfigsPage,
  PluginDetailPage,
  PluginsPage,
  PublishCenterPage,
  ResourceReferenceCard,
  StudioContextProvider,
  VariablesPage,
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
  explorePluginDetailPath,
  exploreTemplateDetailPath,
  inferPrimaryArea,
  replacePathAppKey,
  studioAppsPath,
  studioAppDetailPath,
  studioAppPublishPath,
  studioAssistantDetailPath,
  studioAssistantToolsPath,
  studioAssistantsPath,
  studioAssistantPublishPath,
  studioDataPath,
  studioDatabasesPath,
  studioDatabaseDetailPath,
  studioKnowledgeBaseDetailPath,
  studioKnowledgeBaseUploadPath,
  studioKnowledgeBasesPath,
  studioLibraryPath,
  studioPluginDetailPath,
  studioPluginsPath,
  studioPublishCenterPath,
  studioVariablesPath,
  workflowEditorPath,
  workflowListPath,
  workspaceBasePath,
  workspaceBotPath,
  workspaceChatPath,
  workspaceDevelopPath,
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
import { resolveAppInstanceId } from "@/services/app-instance-context";
import {
  exportLibraryItem,
  getLibraryPaged,
  importLibraryItem,
  moveLibraryItem
} from "@/services/api-ai-workspace";
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
  getOrganizationOverview,
} from "@/services/api-org-management";
import { getStudioWorkspaceOverview } from "@/services/studio-workspace-overview";
import {
  deleteAiApp,
  getAiAppBuilderConfig,
  getAiAppById,
  createAiAppConversationTemplate,
  deleteAiAppConversationTemplate,
  getAiAppConversationTemplates,
  getAiAppPublishRecords,
  getAiAppVersionCheck,
  publishAiApp,
  runAiAppPreview,
  updateAiAppBuilderConfig,
  updateAiApp
} from "@/services/api-ai-app";
import {
  createWorkspaceIdeApp,
  getWorkspaceDashboardStats,
  getWorkspacePublishCenterItems,
  getWorkspaceResourceReferences,
  getWorkspaceIdeSummary,
  recordWorkspaceIdeActivity,
  updateWorkspaceIdeFavorite,
  getWorkspaceIdeResources
} from "@/services/api-workspace-ide";
import {
  getAiPluginBuiltInMetadata,
  getMarketplaceProductById,
  getMarketplaceProductsPaged,
  getAiPluginById,
  getAiPluginsPaged,
  createAiPlugin,
  deleteAiPlugin,
  favoriteMarketplaceProduct,
  getTemplateById,
  publishAiPlugin,
  instantiateTemplate,
  markMarketplaceProductDownloaded,
  getTemplatesPaged,
  getRecentAiEdits,
  searchAi,
  unfavoriteMarketplaceProduct,
  updateAiPlugin
} from "@/services/api-explore";
import {
  createAiDatabase,
  createAiDatabaseRecord,
  downloadAiDatabaseTemplate,
  deleteAiDatabaseRecord,
  getAiDatabasesPaged,
  deleteAiDatabase,
  getAiDatabaseById,
  getAiDatabaseImportProgress,
  getAiDatabaseRecordsPaged,
  submitAiDatabaseImport,
  updateAiDatabaseRecord,
  updateAiDatabase,
  validateAiDatabaseSchema
} from "@/services/api-ai-database";
import {
  createAiVariable,
  deleteAiVariable,
  getAiSystemVariableDefinitions,
  getAiVariablesPaged,
  updateAiVariable
} from "@/services/api-ai-variable";
import {
  generateByAiAssistant,
  bindAiAssistantWorkflow,
  createAiAssistant,
  getAiAssistantById,
  getAiAssistantsPaged,
  getAiAssistantPublications,
  publishAiAssistant,
  regenerateAiAssistantEmbedToken,
  updateAiAssistant
} from "@/services/api-ai-assistant";
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
  clearConversationContext,
  clearConversationHistory,
  createAgentChatStream,
  createConversation,
  deleteConversation,
  getConversationsPaged,
  getMessages
} from "@/services/api-conversation";
import {
  copyWorkflow,
  createWorkflow as createWorkflowDefinition,
  deleteWorkflow as deleteWorkflowDefinition,
  getWorkflowDependencies,
  listWorkflowVersions,
  listWorkflows,
  saveWorkflowDraft,
  workflowV2Api
} from "@/services/api-workflow";
import { executeWorkflowTask } from "@/services/api-workflow-playground";

const libraryApi: LibraryKnowledgeApi = {
  listLibrary: (request, resourceType) => {
    const normalizedType =
      resourceType === "workflow" ||
      resourceType === "plugin" ||
      resourceType === "knowledge-base" ||
      resourceType === "database"
        ? resourceType
        : undefined;

    return getLibraryPaged(request, normalizedType);
  },
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
  runRetrievalTest: testKnowledgeRetrieval,
  getApplicationDetail: async (appId) => {
    const detail = await getAiAppById(String(appId));
    return {
      id: Number(detail.id),
      workflowId: detail.workflowId ? Number(detail.workflowId) : null
    };
  },
  downloadDatabaseTemplate: downloadAiDatabaseTemplate,
  publishPlugin: publishAiPlugin
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
  const createEmptyOrganizationOverview = (appInstanceId: string | null) => ({
    appId: appInstanceId ?? "",
    memberCount: 0,
    roleCount: 0,
    departmentCount: 0,
    positionCount: 0,
    projectCount: 0,
    uncoveredMemberCount: 0,
    recentMembers: [],
    recentRoles: [],
    recentDepartments: [],
    recentPositions: []
  });

  return {
    getOrganizationOverview: async () => {
      const appInstanceId = await resolveAppInstanceId(appKey);
      if (!appInstanceId) {
        return createEmptyOrganizationOverview(null);
      }

      try {
        return await getOrganizationOverview(appInstanceId);
      } catch {
        return createEmptyOrganizationOverview(appInstanceId);
      }
    },
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

function inferWorkflowModeFromTemplate(detail: { name: string; description?: string; tags?: string; schemaJson?: string }): "workflow" | "chatflow" {
  const evidence = `${detail.name} ${detail.description ?? ""} ${detail.tags ?? ""} ${detail.schemaJson ?? ""}`.toLowerCase();
  return evidence.includes("chatflow") || evidence.includes("chat_flow") || evidence.includes("对话")
    ? "chatflow"
    : "workflow";
}

function createExploreApi(appKey: string): ExploreModuleApi {
  return {
    listPlugins: async (request, keyword) => {
      const result = await getMarketplaceProductsPaged(request, {
        keyword,
        productType: 4,
        status: 1
      });
      return {
        ...result,
        items: result.items.map(item => ({
          id: item.id,
          name: item.name,
          description: item.summary,
          categoryName: item.categoryName,
          status: item.status,
          version: item.version,
          downloadCount: item.downloadCount,
          favoriteCount: item.favoriteCount,
          isFavorited: item.isFavorited,
          sourceResourceId: undefined,
          publishedAt: item.publishedAt,
          updatedAt: item.updatedAt
        }))
      };
    },
    getPluginDetail: async (productId) => {
      const detail = await getMarketplaceProductById(productId);
      const sourcePlugin = detail.sourceResourceId ? await getAiPluginById(detail.sourceResourceId).catch(() => null) : null;
      return {
        id: detail.id,
        name: detail.name,
        description: detail.description,
        summary: detail.summary,
        icon: detail.icon,
        categoryId: detail.categoryId,
        categoryName: detail.categoryName,
        productType: detail.productType,
        status: detail.status,
        version: detail.version,
        downloadCount: detail.downloadCount,
        favoriteCount: detail.favoriteCount,
        isFavorited: detail.isFavorited,
        sourceResourceId: detail.sourceResourceId,
        publisherUserId: detail.publisherUserId,
        tags: detail.tags,
        publishedAt: detail.publishedAt,
        createdAt: detail.createdAt,
        updatedAt: detail.updatedAt,
        sourcePluginName: sourcePlugin?.name,
        sourcePluginCategory: sourcePlugin?.category,
        sourcePluginApiCount: sourcePlugin?.apis.length
      };
    },
    favoritePlugin: favoriteMarketplaceProduct,
    unfavoritePlugin: unfavoriteMarketplaceProduct,
    importPluginToStudio: async (productId) => {
      const detail = await getMarketplaceProductById(productId);
      if (!detail.sourceResourceId) {
        throw new Error("当前插件市场商品没有关联可导入的源插件。");
      }

      await markMarketplaceProductDownloaded(productId);
      const imported = await importLibraryItem({
        resourceType: "plugin",
        libraryItemId: detail.sourceResourceId
      });
      return {
        importedPluginId: imported.resourceId,
        route: studioPluginDetailPath(appKey, imported.resourceId)
      };
    },
    listBuiltInPlugins: getAiPluginBuiltInMetadata,
    listTemplates: async (request, filters) => {
      const result = await getTemplatesPaged(request, {
        keyword: filters?.keyword,
        category: filters?.category ?? 2
      });
      return {
        ...result,
        items: result.items.map(item => ({
          id: Number(item.id),
          name: item.name,
          category: item.category,
          description: item.description,
          tags: item.tags,
          version: item.version,
          updatedAt: item.updatedAt,
          schemaJson: item.schemaJson
        }))
      };
    },
    getTemplateDetail: async (templateId) => {
      const detail = await getTemplateById(templateId);
      return {
        id: Number(detail.id),
        name: detail.name,
        category: detail.category,
        schemaJson: detail.schemaJson,
        description: detail.description,
        tags: detail.tags,
        isBuiltIn: detail.isBuiltIn,
        version: detail.version,
        createdAt: detail.createdAt,
        updatedAt: detail.updatedAt
      };
    },
    createWorkflowFromTemplate: async (templateId) => {
      const detail = await getTemplateById(templateId);
      const instantiated = await instantiateTemplate(templateId);
      const mode = inferWorkflowModeFromTemplate({
        name: detail.name,
        description: detail.description,
        tags: detail.tags,
        schemaJson: instantiated.schemaJson
      });
      const createResult = await createWorkflowDefinition({
        name: detail.name,
        description: detail.description || undefined,
        mode: mode === "chatflow" ? 1 : 0
      });
      const workflowId = createResult.data ?? "";
      if (!workflowId) {
        throw new Error("创建工作流失败。");
      }

      await saveWorkflowDraft(workflowId, {
        canvasJson: instantiated.schemaJson
      });

      return {
        workflowId,
        mode,
        route: mode === "chatflow"
          ? `/apps/${encodeURIComponent(appKey)}/chat_flow/${encodeURIComponent(workflowId)}/editor`
          : `/apps/${encodeURIComponent(appKey)}/work_flow/${encodeURIComponent(workflowId)}/editor`
      };
    },
    search: searchAi,
    recent: getRecentAiEdits
  };
}

function normalizeExploreLocalPath(appKey: string, path: string): string {
  if (!path) {
    return workspaceDevelopPath(appKey);
  }

  const agentDetailMatch = path.match(/^\/ai\/agents\/([^/]+)\/edit$/);
  if (agentDetailMatch) {
    return studioAssistantDetailPath(appKey, agentDetailMatch[1]);
  }

  const appDetailMatch = path.match(/^\/ai\/apps\/([^/]+)\/edit$/);
  if (appDetailMatch) {
    return studioAppDetailPath(appKey, appDetailMatch[1]);
  }

  if (path.startsWith("/ai/knowledge-bases/")) {
    return `${studioKnowledgeBasesPath(appKey)}/${path.slice("/ai/knowledge-bases/".length)}`;
  }

  if (path.startsWith("/plugins/")) {
    return `${studioPluginsPath(appKey)}/${path.slice("/plugins/".length)}`;
  }

  if (path.startsWith("/databases/")) {
    return `${studioDatabasesPath(appKey)}/${path.slice("/databases/".length)}`;
  }

  if (path.startsWith("/work_flow/") || path.startsWith("/chat_flow/")) {
    return `/apps/${encodeURIComponent(appKey)}${path}`;
  }

  if (path.startsWith("/apps/")) {
    return path;
  }

  return `/apps/${encodeURIComponent(appKey)}${path.startsWith("/") ? path : `/${path}`}`;
}

interface ParsedSseFrame {
  eventType: string;
  payload: string;
  isDone: boolean;
}

function extractSseFrames(buffer: string): { frames: ParsedSseFrame[]; rest: string } {
  const normalizedBuffer = buffer.replaceAll("\r\n", "\n");
  const rawFrames = normalizedBuffer.split("\n\n");
  const rest = rawFrames.pop() ?? "";
  const frames = rawFrames
    .map(parseSseFrame)
    .filter((frame): frame is ParsedSseFrame => frame !== null);

  return { frames, rest };
}

function parseTrailingSseFrame(buffer: string): ParsedSseFrame | null {
  if (!buffer.trim()) {
    return null;
  }

  return parseSseFrame(buffer.replaceAll("\r\n", "\n"));
}

function parseSseFrame(frame: string): ParsedSseFrame | null {
  const lines = frame.split("\n");
  let eventType = "message";
  const dataLines: string[] = [];

  for (const rawLine of lines) {
    const line = rawLine.trimEnd();
    if (line.startsWith("event:")) {
      eventType = line.slice(6).trim();
      continue;
    }

    if (line.startsWith("data:")) {
      dataLines.push(line.slice(5).trimStart());
    }
  }

  const payload = dataLines.join("\n").trim();
  if (!payload) {
    return null;
  }

  return {
    eventType,
    payload,
    isDone: eventType === "done" || payload === "[DONE]"
  };
}

function toAppShellPath(appKey: string, entryRoute: string): string {
  if (!entryRoute) {
    return workspaceDevelopPath(appKey);
  }

  if (entryRoute.startsWith("/apps/")) {
    return entryRoute;
  }

  return `/apps/${encodeURIComponent(appKey)}${entryRoute.startsWith("/") ? entryRoute : `/${entryRoute}`}`;
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
      buffer += decoder.decode();
      const trailingFrame = parseTrailingSseFrame(buffer);
      if (trailingFrame && !trailingFrame.isDone) {
        if (trailingFrame.eventType === "thought") {
          yield { type: "thought", content: trailingFrame.payload };
        } else if (trailingFrame.eventType === "final") {
          yield { type: "final", content: trailingFrame.payload };
        } else {
          yield { type: "chunk", content: trailingFrame.payload };
        }
      }

      break;
    }

    buffer += decoder.decode(value, { stream: true });
    const { frames, rest } = extractSseFrames(buffer);
    buffer = rest;

    for (const frame of frames) {
      if (frame.isDone) {
        return;
      }

      if (frame.eventType === "thought") {
        yield { type: "thought", content: frame.payload };
        continue;
      }

      if (frame.eventType === "final") {
        yield { type: "final", content: frame.payload };
        continue;
      }

      yield { type: "chunk", content: frame.payload };
    }
  }
}

function createStudioApi(appKey: string): StudioModuleApi {
  return {
    listAgents: getAiAssistantsPaged,
    getAgent: getAiAssistantById,
    createAgent: createAiAssistant,
    updateAgent: updateAiAssistant,
    getWorkspaceOverview: () => getStudioWorkspaceOverview(appKey),
    getWorkspaceSummary: getWorkspaceIdeSummary,
    listWorkspaceResources: async params => {
      const result = await getWorkspaceIdeResources(params);
      return {
        ...result,
        items: result.items.map(item => ({
          ...item,
          entryRoute: toAppShellPath(appKey, item.entryRoute)
        }))
      };
    },
    createApplication: async request => {
      const result = await createWorkspaceIdeApp(request);
      return {
        ...result,
        entryRoute: toAppShellPath(appKey, result.entryRoute)
      };
    },
    getApplication: async id => {
      const detail = await getAiAppById(id);
      return {
        id: String(detail.id),
        name: detail.name,
        description: detail.description,
        icon: detail.icon,
        status: String(detail.status),
        publishVersion: detail.publishVersion,
        workflowId: detail.workflowId ? String(detail.workflowId) : undefined,
        updatedAt: detail.updatedAt,
        lastEditedAt: detail.updatedAt,
        badge: detail.publishVersion > 0 ? `v${detail.publishVersion}` : undefined
      };
    },
    updateApplication: (id, request) => updateAiApp(id, request),
    deleteApplication: id => deleteAiApp(id),
    publishApplication: async (id, releaseNote) => {
      await publishAiApp(id, { releaseNote });
      await getAiAppVersionCheck(id);
    },
    getApplicationPublishRecords: async id => {
      const records = await getAiAppPublishRecords(id);
      return records.map(item => ({
        ...item,
        id: String(item.id),
        appId: String(item.appId),
        publishedByUserId: String(item.publishedByUserId)
      }));
    },
    getApplicationConversationTemplates: async id => {
      const templates = await getAiAppConversationTemplates(id);
      return templates.map(item => ({
        ...item,
        id: String(item.id),
        appId: String(item.appId),
        sourceWorkflowId: item.sourceWorkflowId ? String(item.sourceWorkflowId) : undefined,
        connectorId: item.connectorId ? String(item.connectorId) : undefined
      }));
    },
    createApplicationConversationTemplate: (id, request) =>
      createAiAppConversationTemplate(id, request),
    deleteApplicationConversationTemplate: (id, templateId) =>
      deleteAiAppConversationTemplate(id, templateId),
    listVariables: params => getAiVariablesPaged(
      {
        pageIndex: params?.pageIndex ?? 1,
        pageSize: params?.pageSize ?? 20
      },
      {
        keyword: params?.keyword,
        scope: typeof params?.scope === "number" ? params.scope as 0 | 1 | 2 : undefined,
        scopeId: params?.scopeId
      }
    ),
    createVariable: request => createAiVariable({
      ...request,
      scope: request.scope as 0 | 1 | 2
    }),
    updateVariable: (id, request) => updateAiVariable(id, {
      ...request,
      scope: request.scope as 0 | 1 | 2
    }),
    deleteVariable: deleteAiVariable,
    listSystemVariables: getAiSystemVariableDefinitions,
    toggleWorkspaceFavorite: (resourceType, resourceId, isFavorite) =>
      updateWorkspaceIdeFavorite(resourceType, resourceId, isFavorite),
    recordWorkspaceActivity: request =>
      recordWorkspaceIdeActivity(request),
    getAgentPublications: async agentId => {
      const items = await getAiAssistantPublications(agentId);
      return items.map(item => ({
        ...item,
        id: String(item.id),
        agentId: String(item.agentId),
        publishedByUserId: String(item.publishedByUserId)
      }));
    },
    publishAgent: async (agentId, releaseNote) => {
      const result = await publishAiAssistant(agentId, { releaseNote });
      return {
        ...result,
        publicationId: String(result.publicationId),
        agentId: String(result.agentId)
      };
    },
    regenerateAgentEmbedToken: async agentId => {
      const result = await regenerateAiAssistantEmbedToken(agentId);
      return {
        ...result,
        publicationId: String(result.publicationId),
        agentId: String(result.agentId)
      };
    },
    listConversations: agentId => getConversationsPaged(appKey, { pageIndex: 1, pageSize: 20 }, agentId),
    getMessages: conversationId => getMessages(appKey, conversationId),
    createConversation: (agentId, title) => createConversation(appKey, agentId, title),
    deleteConversation: conversationId => deleteConversation(appKey, conversationId),
    clearConversationContext: conversationId => clearConversationContext(appKey, conversationId),
    clearConversationHistory: conversationId => clearConversationHistory(appKey, conversationId),
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
    listPlugins: async () => {
      const result = await getAiPluginsPaged({ pageIndex: 1, pageSize: 50 });
      return result.items.map(item => ({
        id: item.id,
        name: item.name,
        category: item.category,
        status: item.status,
        sourceType: item.sourceType
      }));
    },
    getPluginDetail: async (pluginId) => {
      const detail = await getAiPluginById(pluginId);
      return {
        id: detail.id,
        name: detail.name,
        description: detail.description,
        category: detail.category,
        type: detail.type,
        sourceType: detail.sourceType,
        authType: detail.authType,
        status: detail.status,
        isLocked: detail.isLocked,
        createdAt: detail.createdAt,
        updatedAt: detail.updatedAt,
        publishedAt: detail.publishedAt,
        definitionJson: detail.definitionJson,
        authConfigJson: detail.authConfigJson,
        toolSchemaJson: detail.toolSchemaJson,
        openApiSpecJson: detail.openApiSpecJson,
        apis: detail.apis.map(api => ({
          id: api.id,
          name: api.name,
          method: api.method,
          path: api.path,
          requestSchemaJson: api.requestSchemaJson,
          timeoutSeconds: api.timeoutSeconds,
          isEnabled: api.isEnabled
        }))
      };
    },
    publishPlugin: publishAiPlugin,
    listKnowledgeBases: async () => {
      const result = await getKnowledgeBasesPaged({ pageIndex: 1, pageSize: 50 });
      return result.items.map(item => ({
        id: item.id,
        name: item.name,
        type: item.type
      }));
    },
    getKnowledgeBase: getKnowledgeBaseById,
    listDatabases: async () => {
      const result = await getAiDatabasesPaged({ pageIndex: 1, pageSize: 50 });
      return result.items.map(item => ({
        id: item.id,
        name: item.name,
        botId: item.botId
      }));
    },
    getDatabaseDetail: getAiDatabaseById,
    listDatabaseRecords: (id, params) =>
      getAiDatabaseRecordsPaged(id, {
        pageIndex: params?.pageIndex ?? 1,
        pageSize: params?.pageSize ?? 10
      }),
    createDatabaseRecord: createAiDatabaseRecord,
    updateDatabaseRecord: updateAiDatabaseRecord,
    deleteDatabaseRecord: deleteAiDatabaseRecord,
    validateDatabaseSchemaDraft: validateAiDatabaseSchema,
    submitDatabaseImport: submitAiDatabaseImport,
    getDatabaseImportProgress: getAiDatabaseImportProgress,
    downloadDatabaseTemplate: downloadAiDatabaseTemplate,
    listBotVariables: async (currentBotId: string) => {
      const result = await getAiVariablesPaged(
        { pageIndex: 1, pageSize: 100 },
        { scope: 2, scopeId: Number(currentBotId) }
      );
      return result.items.map(item => ({
        id: item.id,
        key: item.key,
        scopeId: item.scopeId
      }));
    },
    bindAgentWorkflow: (agentId, workflowId) => bindAiAssistantWorkflow(agentId, workflowId),
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
          buffer += decoder.decode();
          const trailingFrame = parseTrailingSseFrame(buffer);
          if (trailingFrame && !trailingFrame.isDone) {
            text += trailingFrame.payload;
          }
          break;
        }

        buffer += decoder.decode(value, { stream: true });
        const { frames, rest } = extractSseFrames(buffer);
        buffer = rest;

        for (const frame of frames) {
          currentEvent = frame.eventType;
          if (frame.isDone) {
            return text.trim();
          }

          const payload = frame.payload;
          if (!payload || currentEvent === "done" || payload === "[DONE]") {
            continue;
          }

          text += payload;
        }
      }

      return text.trim();
    },
    getDashboardStats: async () => {
      const stats = await getWorkspaceDashboardStats();
      return {
        ...stats,
        recentActivities: (stats.recentActivities ?? []).map(item => ({
          ...item,
          resourceId: String(item.resourceId)
        }))
      };
    },
    getResourceReferences: (resourceType, resourceId) =>
      getWorkspaceResourceReferences(resourceType, resourceId),
    getPublishCenterItems: (params) =>
      getWorkspacePublishCenterItems(params?.resourceType),
    getAppBuilderConfig: async (appId) => {
      const config = await getAiAppBuilderConfig(appId);
      return {
        ...config,
        inputs: config.inputs ?? [],
        outputs: config.outputs ?? [],
        layoutMode: config.layoutMode ?? "form"
      };
    },
    updateAppBuilderConfig: (appId, config) =>
      updateAiAppBuilderConfig(appId, config),
    runAppPreview: (appId, inputs) =>
      runAiAppPreview(appId, inputs),
    listPromptTemplates: async () => {
      // Mock implementation
      return { items: [], total: 0, pageIndex: 1, pageSize: 20 };
    }
  };
}

function createWorkflowModuleApi(appKey: string): WorkflowModuleApi {
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
    getDependencies: async (id: string) => {
      const response = await getWorkflowDependencies(id);
      return response.data ?? {
        workflowId: id,
        subWorkflows: [],
        plugins: [],
        knowledgeBases: [],
        databases: [],
        variables: [],
        conversations: []
      };
    },
    listLibrary: getLibraryPaged,
    importLibraryItem,
    exportLibraryItem,
    moveLibraryItem,
    listPlugins: getAiPluginsPaged,
    getPluginDetail: getAiPluginById,
    createPlugin: createAiPlugin,
    updatePlugin: updateAiPlugin,
    deletePlugin: deleteAiPlugin,
    publishPlugin: publishAiPlugin,
    listKnowledgeBases: getKnowledgeBasesPaged,
    getKnowledgeBase: getKnowledgeBaseById,
    createKnowledgeBase,
    updateKnowledgeBase,
    deleteKnowledgeBase,
    listDatabases: getAiDatabasesPaged,
    getDatabaseDetail: getAiDatabaseById,
    createDatabase: createAiDatabase,
    updateDatabase: updateAiDatabase,
    deleteDatabase: deleteAiDatabase,
    validateDatabaseSchema: validateAiDatabaseSchema,
    listVariables: getAiVariablesPaged,
    createVariable: createAiVariable,
    updateVariable: updateAiVariable,
    deleteVariable: deleteAiVariable,
    listSystemVariables: getAiSystemVariableDefinitions,
    listConversations: request => getConversationsPaged(appKey, request),
    createConversation: request => createConversation(appKey, request.agentId, request.title),
    deleteConversation: id => deleteConversation(appKey, id),
    clearConversationContext: id => clearConversationContext(appKey, id),
    clearConversationHistory: id => clearConversationHistory(appKey, id),
    listConversationMessages: conversationId => getMessages(appKey, conversationId),
    appendConversationMessage: (conversationId, request) => appendConversationMessage(appKey, conversationId, request),
    listAgents: async (request, keyword) => getAiAssistantsPaged({
      pageIndex: request.pageIndex,
      pageSize: request.pageSize,
      keyword
    }),
    apiClient: workflowV2Api
  };
}

function useAppApis(appKey: string) {
  return useMemo(() => ({
    adminApi: createAdminApi(appKey),
    exploreApi: createExploreApi(appKey),
    studioApi: createStudioApi(appKey),
    workflowApi: createWorkflowModuleApi(appKey)
  }), [appKey]);
}

function DefaultWorkspaceRedirect() {
  const { appKey = "" } = useParams();
  const bootstrap = useBootstrap();
  return <Navigate to={workspaceDevelopPath(appKey, bootstrap.spaceId)} replace />;
}

function StudioDevelopAliasRedirect() {
  const { appKey = "" } = useParams();
  const bootstrap = useBootstrap();
  return <Navigate to={workspaceDevelopPath(appKey, bootstrap.spaceId)} replace />;
}

function StudioDashboardAliasRedirect() {
  const { appKey = "" } = useParams();
  const { spaceId } = useBootstrap();
  return <Navigate to={`${workspaceBasePath(appKey, spaceId)}/dashboard`} replace />;
}

function StudioAssistantsAliasRedirect() {
  const { appKey = "" } = useParams();
  const { locale } = useAppI18n();
  const { studioApi } = useAppApis(appKey);
  const navigate = useNavigate();
  return (
    <AssistantsPage
      api={studioApi}
      locale={locale}
      onOpenDetail={assistantId => navigate(`/apps/${encodeURIComponent(appKey)}/studio/assistants/${encodeURIComponent(assistantId)}`)}
      onOpenPublish={assistantId => navigate(`/apps/${encodeURIComponent(appKey)}/studio/assistants/${encodeURIComponent(assistantId)}/publish`)}
    />
  );
}

function StudioAppsAliasRedirect() {
  const { appKey = "" } = useParams();
  const { locale } = useAppI18n();
  const { studioApi } = useAppApis(appKey);
  const navigate = useNavigate();
  return (
    <AppsPage
      api={studioApi}
      locale={locale}
      onOpenDetail={appId => navigate(studioAppDetailPath(appKey, appId))}
      onOpenPublish={appId => navigate(studioAppPublishPath(appKey, appId))}
      onOpenWorkflow={workflowId => navigate(workflowEditorPath(appKey, workflowId))}
    />
  );
}

function StudioLibraryAliasRedirect() {
  const { appKey = "", spaceId = "" } = useParams();
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  const bootstrap = useBootstrap();
  const effectiveSpaceId = spaceId || bootstrap.spaceId;
  return <LibraryPage api={libraryApi} locale={locale} appKey={appKey} spaceId={effectiveSpaceId} onNavigate={navigate} />;
}

function StudioVariablesRoute() {
  const { appKey = "" } = useParams();
  const { locale } = useAppI18n();
  const { studioApi } = useAppApis(appKey);
  return <VariablesPage api={studioApi} locale={locale} />;
}

function StudioPluginsRoute() {
  const { appKey = "" } = useParams();
  const { locale } = useAppI18n();
  const navigate = useNavigate();
  const { studioApi } = useAppApis(appKey);
  return (
    <PluginsPage
      api={studioApi}
      locale={locale}
      onOpenLibrary={() => navigate(studioLibraryPath(appKey))}
      onOpenExplore={() => navigate(explorePath(appKey, "plugin"))}
      onOpenDetail={pluginId => navigate(studioPluginDetailPath(appKey, pluginId))}
    />
  );
}

function StudioPluginDetailRoute() {
  const { appKey = "", id = "" } = useParams();
  const { locale } = useAppI18n();
  const navigate = useNavigate();
  const { studioApi } = useAppApis(appKey);
  return (
    <PluginDetailPage
      api={studioApi}
      locale={locale}
      pluginId={Number(id)}
      onOpenLibrary={() => navigate(studioLibraryPath(appKey))}
      onOpenExplore={() => navigate(explorePath(appKey, "plugin"))}
    />
  );
}

function StudioPublishCenterRoute() {
  const { appKey = "" } = useParams();
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  const { studioApi } = useAppApis(appKey);
  return (
    <PublishCenterPage
      api={studioApi}
      locale={locale}
      apiBase={typeof window !== "undefined" ? `${window.location.origin}/api/v1` : "/api/v1"}
      onOpenAgent={id => navigate(studioAssistantDetailPath(appKey, id))}
      onOpenApp={id => navigate(studioAppDetailPath(appKey, id))}
      onOpenWorkflow={id => navigate(workflowEditorPath(appKey, id))}
      onOpenPlugin={id => navigate(studioPluginDetailPath(appKey, id))}
    />
  );
}

function StudioAssistantToolsRoute() {
  const { appKey = "" } = useParams();
  const { locale } = useAppI18n();
  const { studioApi } = useAppApis(appKey);
  return <AiAssistantPage api={studioApi} locale={locale} />;
}

function StudioDataRoute() {
  const { appKey = "" } = useParams();
  const { locale } = useAppI18n();
  const navigate = useNavigate();
  const { studioApi } = useAppApis(appKey);
  return (
    <DataResourcesPage
      api={studioApi}
      locale={locale}
      onOpenLibrary={() => navigate(studioLibraryPath(appKey))}
    />
  );
}

function StudioKnowledgeBasesRoute() {
  const { appKey = "" } = useParams();
  const { locale } = useAppI18n();
  const navigate = useNavigate();
  const { studioApi } = useAppApis(appKey);
  return (
    <KnowledgeBasesPage
      api={studioApi}
      locale={locale}
      onOpenLibrary={() => navigate(studioLibraryPath(appKey))}
      onOpenDetail={knowledgeBaseId => navigate(studioKnowledgeBaseDetailPath(appKey, knowledgeBaseId))}
      onOpenUpload={(knowledgeBaseId, type) => navigate(studioKnowledgeBaseUploadPath(appKey, knowledgeBaseId, {
        type: type === 1 ? "table" : type === 2 ? "image" : "text"
      }))}
    />
  );
}

function StudioKnowledgeBaseDetailRoute() {
  const { appKey = "", id = "" } = useParams();
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  const bootstrap = useBootstrap();
  const { studioApi } = useAppApis(appKey);
  return (
    <KnowledgeDetailPage
      api={libraryApi}
      locale={locale}
      appKey={appKey}
      spaceId={bootstrap.spaceId}
      knowledgeBaseId={Number(id)}
      onNavigate={navigate}
      resourceReferencesSlot={
        <ResourceReferenceCard
          api={studioApi}
          locale={locale}
          resourceType="knowledge-base"
          resourceId={String(id)}
        />
      }
    />
  );
}

function StudioKnowledgeBaseUploadRoute() {
  const { appKey = "", id = "" } = useParams();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  const bootstrap = useBootstrap();
  return (
    <KnowledgeUploadPage
      api={libraryApi}
      locale={locale}
      appKey={appKey}
      spaceId={bootstrap.spaceId}
      knowledgeBaseId={Number(id)}
      initialType={searchParams.get("type")}
      onNavigate={navigate}
    />
  );
}

function StudioDatabasesRoute() {
  const { appKey = "" } = useParams();
  const { locale } = useAppI18n();
  const navigate = useNavigate();
  const { studioApi } = useAppApis(appKey);
  return (
    <DatabasesPage
      api={studioApi}
      locale={locale}
      onOpenLibrary={() => navigate(studioLibraryPath(appKey))}
      onOpenDetail={databaseId => navigate(studioDatabaseDetailPath(appKey, databaseId))}
    />
  );
}

function StudioDatabaseDetailRoute() {
  const { appKey = "", id = "" } = useParams();
  const { locale } = useAppI18n();
  const navigate = useNavigate();
  const { studioApi } = useAppApis(appKey);
  return (
    <DatabaseDetailPage
      api={studioApi}
      locale={locale}
      databaseId={Number(id)}
      onOpenLibrary={() => navigate(studioLibraryPath(appKey))}
    />
  );
}

function WorkflowsAliasRedirect() {
  const { appKey = "" } = useParams();
  return <Navigate to={workflowListPath(appKey)} replace />;
}

function WorkflowEditorAliasRedirect() {
  const { appKey = "", id = "" } = useParams();
  return <Navigate to={workflowEditorPath(appKey, id)} replace />;
}

function ChatflowsAliasRedirect() {
  const { appKey = "" } = useParams();
  return <Navigate to={`/apps/${encodeURIComponent(appKey)}/chat_flow`} replace />;
}

function ChatflowEditorAliasRedirect() {
  const { appKey = "", id = "" } = useParams();
  return <Navigate to={`/apps/${encodeURIComponent(appKey)}/chat_flow/${encodeURIComponent(id)}/editor`} replace />;
}

function AppShellRoute() {
  const { appKey = "" } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  const bootstrap = useBootstrap();
  const auth = useAuth();
  const { locale, setLocale } = useAppI18n();
  const { studioApi } = useAppApis(appKey);
  const activeShellPath = `${location.pathname}${location.search}`;

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
  const navSections: CozeNavSection[] = [
    {
      key: "workspace",
      title: locale === "zh-CN" ? "工作空间" : "Workspace",
      items: [
        { key: "develop", label: locale === "zh-CN" ? "开发台" : "Develop", icon: navGlyph("D"), path: workspaceDevelopPath(appKey, bootstrap.spaceId), testId: "app-sidebar-item-develop" },
        { key: "agents", label: locale === "zh-CN" ? "智能体" : "Agents", icon: navGlyph("A"), path: studioAssistantsPath(appKey), testId: "app-sidebar-item-agents" },
        { key: "workflow", label: locale === "zh-CN" ? "工作流" : "Workflow", icon: navGlyph("W"), path: workflowListPath(appKey), testId: "app-sidebar-item-workflows" },
        { key: "library", label: locale === "zh-CN" ? "资源库" : "Library", icon: navGlyph("L"), path: studioLibraryPath(appKey), testId: "app-sidebar-item-library" },
        { key: "plugins", label: locale === "zh-CN" ? "插件" : "Plugins", icon: navGlyph("PL"), path: studioPluginsPath(appKey), testId: "app-sidebar-item-plugins" },
        { key: "chat", label: locale === "zh-CN" ? "Agent 对话" : "Agent Chat", icon: navGlyph("C"), path: workspaceChatPath(appKey, bootstrap.spaceId), testId: "app-sidebar-item-agent-chat" },
        { key: "model-configs", label: locale === "zh-CN" ? "模型配置" : "Model Configs", icon: navGlyph("M"), path: workspaceModelConfigsPath(appKey, bootstrap.spaceId), testId: "app-sidebar-item-model-configs" }
      ],
      overflowLabel: locale === "zh-CN" ? "更多" : "More",
      overflowTestId: "app-sidebar-section-more-workspace",
      overflowItems: [
        { key: "projects", label: locale === "zh-CN" ? "应用" : "Applications", icon: navGlyph("APP"), path: studioAppsPath(appKey), testId: "app-sidebar-item-projects" },
        { key: "chatflow", label: locale === "zh-CN" ? "对话流" : "Chatflow", icon: navGlyph("CF"), path: `/apps/${encodeURIComponent(appKey)}/chat_flow`, testId: "app-sidebar-item-chatflows" },
        { key: "assistant", label: locale === "zh-CN" ? "AI 助手" : "AI Assistant", icon: navGlyph("AI"), path: studioAssistantToolsPath(appKey), testId: "app-sidebar-item-ai-assistant" },
        { key: "data", label: locale === "zh-CN" ? "数据" : "Data", icon: navGlyph("DT"), path: studioDataPath(appKey), testId: "app-sidebar-item-data" },
        { key: "knowledge-bases", label: locale === "zh-CN" ? "知识库" : "Knowledge Bases", icon: navGlyph("KB"), path: studioKnowledgeBasesPath(appKey), testId: "app-sidebar-item-knowledge-bases" },
        { key: "databases", label: locale === "zh-CN" ? "数据库" : "Databases", icon: navGlyph("DBX"), path: studioDatabasesPath(appKey), testId: "app-sidebar-item-databases" },
        { key: "variables", label: locale === "zh-CN" ? "变量" : "Variables", icon: navGlyph("VAR"), path: studioVariablesPath(appKey), testId: "app-sidebar-item-variables" }
      ]
    },
    {
      key: "explore",
      title: locale === "zh-CN" ? "探索" : "Explore",
      items: [
        { key: "plugin", label: locale === "zh-CN" ? "插件商店" : "Plugin Store", icon: navGlyph("PL"), path: explorePath(appKey, "plugin"), testId: "app-sidebar-item-explore-plugins" },
        { key: "template", label: locale === "zh-CN" ? "模板商店" : "Template Store", icon: navGlyph("TP"), path: explorePath(appKey, "template"), testId: "app-sidebar-item-explore-templates" }
      ]
    },
    {
      key: "admin",
      title: locale === "zh-CN" ? "管理" : "Management",
      items: [
        { key: "overview", label: locale === "zh-CN" ? "组织概览" : "Overview", icon: navGlyph("OV"), path: adminPath(appKey, "overview"), testId: "app-sidebar-item-organization-overview" },
        { key: "users", label: locale === "zh-CN" ? "用户管理" : "Users", icon: navGlyph("U"), path: adminPath(appKey, "users"), testId: "app-sidebar-item-users" },
        { key: "roles", label: locale === "zh-CN" ? "角色管理" : "Roles", icon: navGlyph("R"), path: adminPath(appKey, "roles"), testId: "app-sidebar-item-roles" },
        { key: "departments", label: locale === "zh-CN" ? "部门管理" : "Departments", icon: navGlyph("DP"), path: adminPath(appKey, "departments"), testId: "app-sidebar-item-departments" },
        { key: "positions", label: locale === "zh-CN" ? "职位管理" : "Positions", icon: navGlyph("P"), path: adminPath(appKey, "positions"), testId: "app-sidebar-item-positions" }
      ],
      overflowLabel: locale === "zh-CN" ? "更多" : "More",
      overflowTestId: "app-sidebar-section-more-admin",
      overflowItems: [
        { key: "approval", label: locale === "zh-CN" ? "审批工作台" : "Approval", icon: navGlyph("AP"), path: adminPath(appKey, "approval"), testId: "app-sidebar-item-approval" },
        { key: "reports", label: locale === "zh-CN" ? "报表管理" : "Reports", icon: navGlyph("RP"), path: adminPath(appKey, "reports"), testId: "app-sidebar-item-reports" },
        { key: "dashboards", label: locale === "zh-CN" ? "仪表盘管理" : "Dashboards", icon: navGlyph("DB"), path: adminPath(appKey, "dashboards"), testId: "app-sidebar-item-dashboards" },
        { key: "visualization", label: locale === "zh-CN" ? "运行监控" : "Visualization", icon: navGlyph("VM"), path: adminPath(appKey, "visualization"), testId: "app-sidebar-item-visualization" },
        { key: "settings", label: locale === "zh-CN" ? "设置" : "Settings", icon: navGlyph("S"), path: adminPath(appKey, "settings"), testId: "app-sidebar-item-settings" },
        { key: "profile", label: locale === "zh-CN" ? "个人中心" : "Profile", icon: navGlyph("ME"), path: adminPath(appKey, "profile"), testId: "app-sidebar-item-profile" }
      ]
    }
  ];

  const activeNavItem = navSections
    .flatMap(section => [...section.items, ...(section.overflowItems ?? [])])
    .find(item => activeShellPath === item.path || activeShellPath.startsWith(`${item.path}/`) || activeShellPath.includes(item.path));

  const workspaceLabel = bootstrap.workspaceLabel || appKey;

  const shellHeaderCopyMap: Record<string, { title: string; subtitle: string }> = locale === "zh-CN"
    ? {
        develop: {
          title: "AI 工作台",
          subtitle: `从 ${workspaceLabel} 继续创作、调试与发布你的 AI 资产。`
        },
        agents: {
          title: "智能体创作",
          subtitle: "设计角色、工具、记忆与调试链路，持续打磨智能体体验。"
        },
        projects: {
          title: "应用装配",
          subtitle: "把智能体与工作流组织成可交付、可发布的应用体验。"
        },
        workflow: {
          title: "工作流编排",
          subtitle: "连接节点、数据与工具，构建可运行的自动化流程。"
        },
        chatflow: {
          title: "对话流设计",
          subtitle: "围绕多轮交互编排消息路径、状态流转与用户引导。"
        },
        library: {
          title: "资源接入",
          subtitle: "统一管理可复用的工具、知识、数据与外部能力。"
        },
        plugins: {
          title: "插件能力",
          subtitle: "连接外部工具与 API，为智能体和流程提供可调用能力。"
        },
        chat: {
          title: "调试对话",
          subtitle: "通过真实对话验证 Agent、工具调用与工作流行为。"
        },
        "model-configs": {
          title: "模型接入",
          subtitle: "管理模型供应商、推理能力与运行策略。"
        },
        assistant: {
          title: "AI 助手",
          subtitle: "在工作区里完成问答、生成、整理与协作辅助。"
        },
        data: {
          title: "数据资产",
          subtitle: "集中查看结构化数据、知识与变量等可复用资源。"
        },
        "knowledge-bases": {
          title: "知识库空间",
          subtitle: "沉淀可被智能体与工作流长期复用的知识内容。"
        },
        databases: {
          title: "数据库连接",
          subtitle: "管理数据库资源与运行时数据接入。"
        },
        variables: {
          title: "变量配置",
          subtitle: "维护流程与应用共享的上下文变量和运行参数。"
        },
        overview: {
          title: "团队协作",
          subtitle: "查看空间成员、权限分工与整体治理状态。"
        },
        users: {
          title: "成员管理",
          subtitle: "邀请成员并维护工作区访问范围与协作权限。"
        },
        roles: {
          title: "权限策略",
          subtitle: "通过角色定义协作边界、菜单能力与访问权限。"
        },
        departments: {
          title: "组织结构",
          subtitle: "维护团队层级、归属关系与组织视图。"
        },
        positions: {
          title: "岗位配置",
          subtitle: "定义岗位职责并对齐组织内的权限分配。"
        },
        approval: {
          title: "审批协作",
          subtitle: "跟进业务审批、发布流转与待处理协作任务。"
        },
        reports: {
          title: "运营洞察",
          subtitle: "查看关键报表、业务趋势与阶段性运营结果。"
        },
        dashboards: {
          title: "指标看板",
          subtitle: "聚合核心指标，快速把握空间与业务运行态势。"
        },
        visualization: {
          title: "运行观测",
          subtitle: "监控任务状态、异常波动与运行过程。"
        },
        settings: {
          title: "工作区设置",
          subtitle: "调整空间规则、偏好与系统配置项。"
        },
        profile: {
          title: "个人中心",
          subtitle: "管理账号资料、偏好与个人工作设置。"
        },
        plugin: {
          title: "插件发现",
          subtitle: "浏览可接入的插件能力与生态资源。"
        },
        template: {
          title: "模板发现",
          subtitle: "挑选模板，加速搭建应用、智能体与流程。"
        }
      }
    : {
        develop: {
          title: "AI Workspace",
          subtitle: `Continue creating, debugging, and publishing assets in ${workspaceLabel}.`
        },
        agents: {
          title: "Agent Creation",
          subtitle: "Design roles, tools, memory, and debugging flows for your agents."
        },
        projects: {
          title: "App Assembly",
          subtitle: "Package agents and workflows into deliverable product experiences."
        },
        workflow: {
          title: "Workflow Orchestration",
          subtitle: "Connect nodes, data, and tools into runnable automation flows."
        },
        chatflow: {
          title: "Chatflow Design",
          subtitle: "Shape multi-turn interactions, message routing, and user guidance."
        },
        library: {
          title: "Resource Hub",
          subtitle: "Manage reusable tools, knowledge, data, and external capabilities."
        },
        plugins: {
          title: "Plugin Capabilities",
          subtitle: "Connect external tools and APIs for agents and workflows."
        },
        chat: {
          title: "Debug Chat",
          subtitle: "Validate agent behavior, tool calls, and workflow outcomes through chat."
        },
        "model-configs": {
          title: "Model Access",
          subtitle: "Manage model providers, inference capabilities, and runtime strategy."
        },
        assistant: {
          title: "AI Assistant",
          subtitle: "Use in-workspace AI support for drafting, organizing, and collaboration."
        },
        data: {
          title: "Data Assets",
          subtitle: "Review structured data, knowledge, and variables in one place."
        },
        "knowledge-bases": {
          title: "Knowledge Space",
          subtitle: "Maintain reusable knowledge for agents and workflows."
        },
        databases: {
          title: "Database Connections",
          subtitle: "Manage databases and runtime data access."
        },
        variables: {
          title: "Variable Config",
          subtitle: "Maintain shared context variables and runtime parameters."
        },
        overview: {
          title: "Team Collaboration",
          subtitle: "View members, permissions, and the overall governance state."
        },
        users: {
          title: "Member Management",
          subtitle: "Invite members and manage workspace access scopes."
        },
        roles: {
          title: "Permission Strategy",
          subtitle: "Define collaboration boundaries and access policies with roles."
        },
        departments: {
          title: "Organization Structure",
          subtitle: "Maintain team hierarchy, ownership, and structure."
        },
        positions: {
          title: "Position Setup",
          subtitle: "Define job responsibilities and align permission assignments."
        },
        approval: {
          title: "Approval Collaboration",
          subtitle: "Track approval flows, releases, and pending collaboration tasks."
        },
        reports: {
          title: "Operational Insights",
          subtitle: "Review reports, trends, and milestone outcomes."
        },
        dashboards: {
          title: "Metrics Dashboard",
          subtitle: "Monitor the most important signals in one place."
        },
        visualization: {
          title: "Runtime Observability",
          subtitle: "Monitor jobs, anomalies, and execution status across the workspace."
        },
        settings: {
          title: "Workspace Settings",
          subtitle: "Adjust workspace rules, preferences, and system options."
        },
        profile: {
          title: "Profile Center",
          subtitle: "Manage your account profile, preferences, and personal settings."
        },
        plugin: {
          title: "Plugin Discovery",
          subtitle: "Explore ecosystem plugins and integration options."
        },
        template: {
          title: "Template Discovery",
          subtitle: "Browse templates to speed up apps, agents, and workflows."
        }
      };

  const defaultHeaderCopy = primaryKey === "workspace"
    ? (locale === "zh-CN"
      ? {
          title: "AI 工作空间",
          subtitle: `围绕 ${workspaceLabel} 统一完成创作、运行与团队协作。`
        }
      : {
          title: "AI Workspace",
          subtitle: `Create, operate, and collaborate around ${workspaceLabel} in one place.`
        })
    : primaryKey === "explore"
      ? (locale === "zh-CN"
        ? {
            title: "生态发现",
            subtitle: "浏览模板、插件与可复用生态资源。"
          }
        : {
            title: "Explore Ecosystem",
            subtitle: "Browse templates, plugins, and reusable ecosystem resources."
          })
      : (locale === "zh-CN"
        ? {
            title: "团队治理",
            subtitle: "统一管理成员、权限、运营与空间设置。"
          }
        : {
            title: "Team Governance",
            subtitle: "Manage members, permissions, operations, and workspace settings."
          });

  const shellHeaderCopy = activeNavItem
    ? shellHeaderCopyMap[activeNavItem.key] ?? defaultHeaderCopy
    : defaultHeaderCopy;

  return (
    <>
      <UnauthorizedNavigationBridge />
      <CozeShell
        appKey={appKey}
        workspaceLabel={bootstrap.workspaceLabel || appKey}
        activePath={activeShellPath}
        navSections={navSections}
        headerTitle={shellHeaderCopy.title}
        headerSubtitle={shellHeaderCopy.subtitle}
        localeLabel={locale === "zh-CN" ? "English" : "中文"}
        userName={auth.profile?.displayName || auth.profile?.username || "Atlas"}
        profileLabel={locale === "zh-CN" ? "个人中心" : "Profile"}
        logoutLabel={locale === "zh-CN" ? "退出登录" : "Sign Out"}
        onNavigate={navigate}
        onToggleLocale={() => setLocale(locale === "zh-CN" ? "en-US" : "zh-CN")}
        onOpenProfile={() => navigate(adminPath(appKey, "profile"))}
        onLogout={() => {
          void auth.logout().then(() => navigate(appSignPath(appKey), { replace: true }));
        }}
      >
        <StudioContextProvider api={studioApi}>
          <Outlet />
        </StudioContextProvider>
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
      onOpenUsers={() => navigate(adminPath(appKey, "users"))}
      onOpenRoles={() => navigate(adminPath(appKey, "roles"))}
      onOpenDepartments={() => navigate(adminPath(appKey, "departments"))}
      onOpenPositions={() => navigate(adminPath(appKey, "positions"))}
      onOpenLibrary={() => navigate(studioLibraryPath(appKey))}
      onOpenApplicationDetail={appId => navigate(studioAppDetailPath(appKey, appId))}
      onOpenApplicationPublish={appId => navigate(studioAppPublishPath(appKey, appId))}
      onCreateWorkflow={() => navigate(`${workflowListPath(appKey)}?create=1`)}
      onCreateChatflow={() => navigate(`/apps/${encodeURIComponent(appKey)}/chat_flow?create=1`)}
    />
  );
}

function DashboardRoute() {
  const { appKey = "" } = useParams();
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  const { studioApi } = useAppApis(appKey);
  const { spaceId } = useBootstrap();
  return (
    <DashboardPage
      api={studioApi}
      locale={locale}
      onNavigateToResource={(type, id) => {
        if (type === "agent") navigate(studioAssistantDetailPath(appKey, id));
        else if (type === "app") navigate(studioAppDetailPath(appKey, id));
        else if (type === "workflow") navigate(workflowEditorPath(appKey, id));
        else if (type === "plugin") navigate(studioPluginDetailPath(appKey, id));
      }}
      onNavigateToModels={() => navigate(workspaceModelConfigsPath(appKey, spaceId))}
      onNavigateToPublish={() => navigate(studioPublishCenterPath(appKey))}
      onCreateAgent={() => {
        // Simple routing mapping for now
        navigate(studioAssistantsPath(appKey));
      }}
      onCreateApp={() => navigate(studioAppsPath(appKey))}
      onCreateWorkflow={() => navigate(`${workflowListPath(appKey)}?create=1`)}
    />
  );
}

function BotIdeRoute() {
  const { appKey = "", botId = "" } = useParams();
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  const { studioApi } = useAppApis(appKey);
  return (
    <BotIdePage
      api={studioApi}
      locale={locale}
      botId={botId}
      onOpenPublish={() => navigate(studioAssistantPublishPath(appKey, botId))}
    />
  );
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

function StudioAppDetailRoute() {
  const { appKey = "", id = "" } = useParams();
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  const { studioApi } = useAppApis(appKey);
  return (
    <AppDetailPage
      api={studioApi}
      locale={locale}
      appId={id}
      onOpenWorkflow={workflowId => navigate(workflowEditorPath(appKey, workflowId))}
      onOpenPublish={() => navigate(studioAppPublishPath(appKey, id))}
    />
  );
}

function StudioAppPublishRoute() {
  const { appKey = "", id = "" } = useParams();
  const { locale } = useAppI18n();
  const { studioApi } = useAppApis(appKey);
  return <AppPublishPage api={studioApi} locale={locale} appId={id} />;
}

function StudioAssistantDetailRoute() {
  const { appKey = "", id = "" } = useParams();
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  const { studioApi } = useAppApis(appKey);
  return (
    <BotIdePage
      api={studioApi}
      locale={locale}
      botId={id}
      onOpenPublish={() => navigate(studioAssistantPublishPath(appKey, id))}
    />
  );
}

function StudioAssistantPublishRoute() {
  const { appKey = "", id = "" } = useParams();
  const { locale } = useAppI18n();
  const { studioApi } = useAppApis(appKey);
  return <AssistantPublishPage api={studioApi} locale={locale} assistantId={id} />;
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

function AdminOverviewRoute() {
  const { appKey = "" } = useParams();
  const { locale } = useAppI18n();
  const { adminApi } = useAppApis(appKey);
  return <OrganizationOverviewPage api={adminApi} locale={locale} />;
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
  const navigate = useNavigate();
  return (
    <ExplorePluginsPage
      api={exploreApi}
      locale={locale}
      onOpenDetail={productId => navigate(explorePluginDetailPath(appKey, productId))}
      onOpenImported={route => navigate(route)}
    />
  );
}

function ExploreTemplatesRoute() {
  const { appKey = "" } = useParams();
  const { locale } = useAppI18n();
  const { exploreApi } = useAppApis(appKey);
  const navigate = useNavigate();
  return (
    <ExploreTemplatesPage
      api={exploreApi}
      locale={locale}
      onOpenDetail={templateId => navigate(exploreTemplateDetailPath(appKey, templateId))}
      onOpenCreated={route => navigate(route)}
    />
  );
}

function ExploreSearchRoute() {
  const { appKey = "", word = "" } = useParams();
  const { locale } = useAppI18n();
  const { exploreApi } = useAppApis(appKey);
  const navigate = useNavigate();
  return <ExploreSearchPage api={exploreApi} locale={locale} keyword={decodeURIComponent(word)} onOpenLocal={path => navigate(normalizeExploreLocalPath(appKey, path))} />;
}

function ExplorePluginDetailRoute() {
  const { appKey = "", productId = "" } = useParams();
  const { locale } = useAppI18n();
  const { exploreApi } = useAppApis(appKey);
  const navigate = useNavigate();
  return (
    <ExplorePluginDetailPage
      api={exploreApi}
      locale={locale}
      productId={Number(productId)}
      onOpenImported={route => navigate(route)}
    />
  );
}

function ExploreTemplateDetailRoute() {
  const { appKey = "", templateId = "" } = useParams();
  const { locale } = useAppI18n();
  const { exploreApi } = useAppApis(appKey);
  const navigate = useNavigate();
  return (
    <ExploreTemplateDetailPage
      api={exploreApi}
      locale={locale}
      templateId={Number(templateId)}
      onOpenCreated={route => navigate(route)}
    />
  );
}

export function AppRouter() {
  return (
    <BrowserRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/platform-not-ready" element={<PlatformNotReadyPage />} />
        <Route path="/app-setup" element={<AppSetupPage />} />
        <Route path="/apps/:appKey/sign" element={<LoginPage />} />
        <Route path="/apps/:appKey" element={<AppShellRoute />}>
          <Route index element={<DefaultWorkspaceRedirect />} />
          <Route path="studio/dashboard" element={<StudioDashboardAliasRedirect />} />
          <Route path="studio/develop" element={<StudioDevelopAliasRedirect />} />
          <Route path="studio/assistants" element={<StudioAssistantsAliasRedirect />} />
          <Route path="studio/assistants/:id" element={<StudioAssistantDetailRoute />} />
          <Route path="studio/assistants/:id/publish" element={<StudioAssistantPublishRoute />} />
          <Route path="studio/apps" element={<StudioAppsAliasRedirect />} />
          <Route path="studio/apps/:id" element={<StudioAppDetailRoute />} />
          <Route path="studio/apps/:id/publish" element={<StudioAppPublishRoute />} />
          <Route path="studio/library" element={<StudioLibraryAliasRedirect />} />
          <Route path="studio/plugins/:id" element={<StudioPluginDetailRoute />} />
          <Route path="studio/plugins" element={<StudioPluginsRoute />} />
          <Route path="studio/assistant-tools" element={<StudioAssistantToolsRoute />} />
          <Route path="studio/data" element={<StudioDataRoute />} />
          <Route path="studio/knowledge-bases/:id/upload" element={<StudioKnowledgeBaseUploadRoute />} />
          <Route path="studio/knowledge-bases/:id" element={<StudioKnowledgeBaseDetailRoute />} />
          <Route path="studio/knowledge-bases" element={<StudioKnowledgeBasesRoute />} />
          <Route path="studio/databases/:id" element={<StudioDatabaseDetailRoute />} />
          <Route path="studio/databases" element={<StudioDatabasesRoute />} />
          <Route path="studio/publish-center" element={<StudioPublishCenterRoute />} />
          <Route path="studio/variables" element={<StudioVariablesRoute />} />
          <Route path="workflows" element={<WorkflowsAliasRedirect />} />
          <Route path="workflows/:id/editor" element={<WorkflowEditorAliasRedirect />} />
          <Route path="chatflows" element={<ChatflowsAliasRedirect />} />
          <Route path="chatflows/:id/editor" element={<ChatflowEditorAliasRedirect />} />
          <Route path="space/:spaceId/dashboard" element={<DashboardRoute />} />
          <Route path="space/:spaceId/develop" element={<DevelopRoute />} />
          <Route path="space/:spaceId/chat" element={<AgentChatRoute />} />
          <Route path="space/:spaceId/assistant" element={<AiAssistantRoute />} />
          <Route path="space/:spaceId/model-configs" element={<ModelConfigsRoute />} />
          <Route path="space/:spaceId/bot/:botId" element={<BotIdeRoute />} />
          <Route path="space/:spaceId/library" element={<ProtectedPage permission={APP_PERMISSIONS.KNOWLEDGE_BASE_VIEW}><LibraryRoute /></ProtectedPage>} />
          <Route path="space/:spaceId/knowledge/:id" element={<ProtectedPage permission={APP_PERMISSIONS.KNOWLEDGE_BASE_VIEW}><KnowledgeDetailRoute /></ProtectedPage>} />
          <Route path="space/:spaceId/knowledge/:id/upload" element={<ProtectedPage permission={APP_PERMISSIONS.KNOWLEDGE_BASE_UPDATE}><KnowledgeUploadRoute /></ProtectedPage>} />
          <Route path="explore/plugin/:productId" element={<ExplorePluginDetailRoute />} />
          <Route path="explore/plugin" element={<ExplorePluginsRoute />} />
          <Route path="explore/template/:templateId" element={<ExploreTemplateDetailRoute />} />
          <Route path="explore/template" element={<ExploreTemplatesRoute />} />
          <Route path="search/:word" element={<ExploreSearchRoute />} />
          <Route path="entry" element={<EntryGatewayPage />} />
          <Route path="work_flow" element={<WorkflowListRoute mode="workflow" />} />
          <Route path="work_flow/:id/editor" element={<WorkflowEditorRoute mode="workflow" />} />
          <Route path="chat_flow" element={<WorkflowListRoute mode="chatflow" />} />
          <Route path="chat_flow/:id/editor" element={<WorkflowEditorRoute mode="chatflow" />} />
          <Route path="admin/overview" element={<AdminOverviewRoute />} />
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

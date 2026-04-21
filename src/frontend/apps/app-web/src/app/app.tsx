/* eslint-disable @typescript-eslint/no-unused-vars */
import * as React from "react";
import { Component, Suspense, useCallback, useEffect, useMemo, useState, type ErrorInfo, type ReactElement, type ReactNode } from "react";
import { createBrowserRouter, Navigate, Outlet, RouterProvider, useLocation, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { Button, Card, Typography } from "@douyinfe/semi-ui";
import { getTenantId } from "@atlas/shared-react-core/utils";
import { PageShell, ResultCard } from "./_shared";

const { Title, Text } = Typography;
import type { CozeNavSection } from "@atlas/coze-shell-react";
import type { LibraryKnowledgeApi } from "@atlas/library-module-react";
import {
  createMockLibraryApi,
  KnowledgeBaseCreateWizard,
  KnowledgeJobsCenterPage,
  KnowledgeProviderConfigPage
} from "@atlas/library-module-react";
import type {
  AdminModuleApi
} from "@atlas/module-admin-react";
import type { ExploreModuleApi } from "@atlas/module-explore-react";
import type {
  DevelopFocus,
  DevelopResourceSummary,
  StudioModuleApi
} from "@atlas/module-studio-react";
import {
  adminPath,
  appForbiddenPath,
  appSignPath,
  communityWorksPath,
  explorePath,
  explorePluginDetailPath,
  exploreTemplateDetailPath,
  inferPrimaryArea,
  marketPluginsPath,
  marketTemplatesPath,
  meProfilePath,
  openApiPath,
  orgWorkspaceAssistantToolsPath,
  orgWorkspaceChatPath,
  orgWorkspaceDataPath,
  orgWorkspaceHomePath,
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
  workflowListPath,
  workspaceBasePath,
  workspaceBotPath,
  workspaceChatPath,
  workspaceDevelopPath,
  workspaceModelConfigsPath,
  orgWorkspacesPath,
  orgWorkspaceDevelopPath,
  orgWorkspaceLibraryPath,
  orgWorkspaceManagePath,
  orgWorkspaceModelConfigsPath,
  orgWorkspacePublishCenterPath,
  orgWorkspaceVariablesPath,
  orgWorkspaceAppDetailPath,
  orgWorkspaceAppPublishPath,
  orgWorkspaceAgentDetailPath,
  orgWorkspaceAgentPublishPath,
  orgWorkspaceAppWorkflowPath,
  orgWorkspaceAppChatflowPath,
  orgWorkspaceWorkflowsPath,
  orgWorkspaceChatflowsPath,
  orgWorkspaceWorkflowPath,
  orgWorkspaceChatflowPath,
  orgWorkspaceKnowledgeBaseDetailPath,
  orgWorkspaceKnowledgeBaseUploadPath,
  orgWorkspaceDatabaseDetailPath,
  orgWorkspacePluginDetailPath,
  orgWorkspaceSettingsPath,
  signPath
} from "@atlas/app-shell-shared";
import { lazyNamed } from "./lazy-named";
import type { AppLocale } from "./messages";
import { AuthProvider, useAuth } from "./auth-context";
import { BootstrapProvider, useBootstrap } from "./bootstrap-context";
import { AppI18nProvider, useAppI18n } from "./i18n";
import { OrganizationProvider, useOptionalOrganizationContext } from "./organization-context";
import {
  EXPLORE_ROUTE_HANDLE,
  ROOT_ROUTE_HANDLE,
  SETUP_CONSOLE_ROUTE_HANDLE,
  SIGN_ROUTE_HANDLE,
  STANDALONE_WORKFLOW_ROUTE_HANDLE,
  STATUS_ROUTE_HANDLE,
  WORKSPACE_CHATFLOW_ROUTE_HANDLE,
  WORKSPACE_DASHBOARD_ROUTE_HANDLE,
  WORKSPACE_DEVELOP_ROUTE_HANDLE,
  WORKSPACE_LIBRARY_ROUTE_HANDLE,
  WORKSPACE_LIST_ROUTE_HANDLE,
  WORKSPACE_MANAGE_ROUTE_HANDLE,
  WORKSPACE_SETTINGS_ROUTE_HANDLE,
  WORKSPACE_SHELL_ROUTE_HANDLE,
  WORKSPACE_WORKFLOW_ROUTE_HANDLE
} from "./route-handles";
import { AppStartupKernel } from "./startup-kernel";
import { WorkflowRuntimeBoundary } from "./workflow-runtime-boundary";
import { WorkspaceProvider, useOptionalWorkspaceContext, useWorkspaceContext } from "./workspace-context";
import {
  buildWorkspaceWorkbenchPath,
  resolveLegacyAppRedirectTarget
} from "./legacy-route-mapping";
import { APP_PERMISSIONS } from "../constants/permissions";
import {
  getConfiguredAppKey,
  requestApi,
  rememberConfiguredAppKey,
  setUnauthorizedHandler
} from "../services/api-core";
import { resolveAppInstanceId } from "../services/app-instance-context";
import {
  exportLibraryItem,
  getLibraryPaged,
  importLibraryItem,
  moveLibraryItem
} from "../services/api-ai-workspace";
import {
  cancelKnowledgeJob,
  createChunk,
  createKnowledgeBase,
  createKnowledgeBinding,
  createKnowledgeDocumentByFile,
  createKnowledgeVersionSnapshot,
  deleteChunk,
  deleteKnowledgeBase,
  deleteKnowledgeDocument,
  diffKnowledgeVersions,
  getDocumentChunksPaged,
  getDocumentProgress,
  getKnowledgeBaseById,
  getKnowledgeBasesPaged,
  getKnowledgeDocumentsPaged,
  getKnowledgeJob,
  getKnowledgeRetrievalLog,
  grantKnowledgePermission,
  listAllKnowledgeBindings,
  listAllKnowledgeJobs,
  listKnowledgeBindings,
  listKnowledgeImageItems,
  listKnowledgeJobs,
  listKnowledgePermissions,
  listKnowledgeProviderConfigs,
  listKnowledgeRetrievalLogs,
  listKnowledgeTableColumns,
  listKnowledgeTableRows,
  listKnowledgeVersions,
  rebuildKnowledgeIndex,
  releaseKnowledgeVersion,
  removeKnowledgeBinding,
  rerunParseJob,
  resegmentDocument,
  retryKnowledgeJob,
  revokeKnowledgePermission,
  rollbackKnowledgeVersion,
  runKnowledgeRetrieval,
  testKnowledgeRetrieval,
  updateChunk,
  updateKnowledgeBase,
  updateKnowledgeChunkingProfile,
  updateKnowledgeRetrievalProfile
} from "../services/api-knowledge";
import { HomePage } from "./pages/home-page";
import { LoginPage } from "./pages/login-page";
import { EntryGatewayPage } from "./pages/entry-gateway-page";
import { ForbiddenPage } from "./pages/forbidden-page";
import { AppSetupPage, PlatformNotReadyPage } from "./pages/status-page";
import { SetupConsolePage } from "./pages/setup-console";
import { WorkspaceShellLayout, PlatformShellLayout, readLastWorkspaceId, rememberLastWorkspaceId } from "./layouts/workspace-shell";
import { EditorShellLayout } from "./layouts/editor-shell";
import { WorkspaceSwitcher } from "./components/workspace-switcher";
import { GlobalCreateModal } from "./components/global-create-modal";
import {
  AgentEditorRoute,
  AgentPublishRoute,
  AppEditorRoute,
  AppPublishRoute,
  ChatflowEditorRoute,
  WorkflowEditorRoute
} from "./pages/editor-routes";
import { WorkspaceHomePage } from "./pages/workspace-home-page";
import { WorkspaceProjectsPage } from "./pages/workspace-projects-page";
import { WorkspaceResourcesPage } from "./pages/workspace-resources-page";
import { WorkspaceTasksPage } from "./pages/workspace-tasks-page";
import { WorkspaceEvaluationsPage } from "./pages/workspace-evaluations-page";
import { WorkspaceSettingsPublishPage } from "./pages/workspace-settings-publish-page";
import { WorkspaceSettingsModelsPage } from "./pages/workspace-settings-models-page";
import { MarketTemplatesPage } from "./pages/market-templates-page";
import { MarketPluginsPage } from "./pages/market-plugins-page";
import { CommunityWorksPage } from "./pages/community-works-page";
import { OpenApiPage } from "./pages/open-api-page";
import { DocsPage } from "./pages/docs-page";
import { PlatformGeneralPage } from "./pages/platform-general-page";
import { MeProfilePage } from "./pages/me-profile-page";
import { MeSettingsPage } from "./pages/me-settings-page";
import { MeNotificationsPage } from "./pages/me-notifications-page";
import { SelectWorkspacePage } from "./pages/select-workspace-page";
import {
  backupNow,
  getDatabaseInfo,
  listBackups,
  testConnection
} from "../services/api-db-maintenance";
import {
  getMyCopyRecordsPaged,
  getMyInstancesPaged,
  getMyTasksPaged
} from "../services/api-approval";
import {
  createDashboard,
  createReport,
  deleteDashboard,
  deleteReport,
  getDashboardsPaged,
  getReportsPaged,
  updateDashboard,
  updateReport
} from "../services/api-reports";
import { getVisualizationInstances } from "../services/api-visualization";
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
} from "../services/api-admin";
import {
  getOrganizationOverview,
} from "../services/api-org-management";
import { getStudioWorkspaceOverview } from "../services/studio-workspace-overview";
import {
  getWorkspaceChannelActiveRelease,
  listWorkspacePublishChannels,
  publishChannelsHttpJson
} from "../services/api-publish-channels";
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
} from "../services/api-ai-app";
import {
  createWorkspaceIdeApp,
  getWorkspaceDashboardStats,
  getWorkspacePublishCenterItems,
  getWorkspaceResourceReferences,
  getWorkspaceIdeSummary,
  recordWorkspaceIdeActivity,
  updateWorkspaceIdeFavorite,
  getWorkspaceIdeResources
} from "../services/api-workspace-ide";
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
} from "../services/api-explore";
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
  validateAiDatabaseSchema,
  createAiDatabaseRecordsBulk,
  submitAiDatabaseBulkInsertJob
} from "../services/api-ai-database";
import {
  createAiVariable,
  deleteAiVariable,
  getAiSystemVariableDefinitions,
  getAiVariablesPaged,
  updateAiVariable
} from "../services/api-ai-variable";
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
} from "../services/api-ai-assistant";
import {
  createModelConfig,
  createModelConfigPromptTestStream,
  deleteModelConfig,
  getModelConfigById,
  getModelConfigStats,
  getModelConfigsPaged,
  testModelConfigConnection,
  updateModelConfig
} from "../services/api-model-config";
import {
  appendConversationMessage,
  clearConversationContext,
  clearConversationHistory,
  createAgentChatStream,
  createConversation,
  deleteConversation,
  getConversationsPaged,
  getMessages
} from "../services/api-conversation";
import {
  createWorkflow as createWorkflowDefinition,
  listWorkflows,
  type WorkflowListItem,
  saveWorkflowDraft
} from "../services/api-workflow";
import {
  addWorkspaceMember,
  createWorkspace,
  createWorkspaceAppInstance,
  deleteWorkspace,
  getWorkspaceByAppKey,
  getWorkspaceMembers,
  getWorkspaceResourcePermissions,
  getWorkspaceResources,
  getWorkspaces,
  removeWorkspaceMember,
  type WorkspaceCreateRequest,
  type WorkspaceMemberDto,
  type WorkspaceResourceCardDto,
  type WorkspaceRolePermissionDto,
  type WorkspaceUpdateRequest,
  updateWorkspace,
  updateWorkspaceMemberRole,
  updateWorkspaceResourcePermissions
} from "../services/api-org-workspaces";
import { OrganizationWorkspacesPage } from "./pages/organization-workspaces-page";
import { WorkspaceSettingsPage } from "./pages/workspace-settings-page";
import { setAppInstanceIdToStorage } from "../utils/app-context";

type WorkflowResourceMode = "workflow" | "chatflow";
type WorkflowWorkbenchContentMode = "canvas" | "variables" | "session";

const loadCozeShellModule = () => import("@atlas/coze-shell-react");
const loadLibraryModule = () => import("@atlas/library-module-react");
const loadAdminModule = () => import("@atlas/module-admin-react");
const loadExploreModule = () => import("@atlas/module-explore-react");
const loadStudioModule = () => import("@atlas/module-studio-react");
const loadCozeWorkflowPlaygroundModule = () => import("@coze-workflow/playground-adapter");


const CozeShell = lazyNamed(loadCozeShellModule, "CozeShell");
const LibraryPage = lazyNamed(loadLibraryModule, "LibraryPage");
const KnowledgeDetailPage = lazyNamed(loadLibraryModule, "KnowledgeDetailPage");
const KnowledgeUploadPage = lazyNamed(loadLibraryModule, "KnowledgeUploadPage");
const OrganizationOverviewPage = lazyNamed(loadAdminModule, "OrganizationOverviewPage");
const ApprovalAdminPage = lazyNamed(loadAdminModule, "ApprovalAdminPage");
const DashboardsAdminPage = lazyNamed(loadAdminModule, "DashboardsAdminPage");
const DepartmentsAdminPage = lazyNamed(loadAdminModule, "DepartmentsAdminPage");
const ProfileAdminPage = lazyNamed(loadAdminModule, "ProfileAdminPage");
const ReportsAdminPage = lazyNamed(loadAdminModule, "ReportsAdminPage");
const RolesAdminPage = lazyNamed(loadAdminModule, "RolesAdminPage");
const SettingsAdminPage = lazyNamed(loadAdminModule, "SettingsAdminPage");
const UsersAdminPage = lazyNamed(loadAdminModule, "UsersAdminPage");
const VisualizationAdminPage = lazyNamed(loadAdminModule, "VisualizationAdminPage");
const PositionsAdminPage = lazyNamed(loadAdminModule, "PositionsAdminPage");
const ExplorePluginsPage = lazyNamed(loadExploreModule, "ExplorePluginsPage");
const ExploreSearchPage = lazyNamed(loadExploreModule, "ExploreSearchPage");
const ExploreTemplatesPage = lazyNamed(loadExploreModule, "ExploreTemplatesPage");
const ExplorePluginDetailPage = lazyNamed(loadExploreModule, "ExplorePluginDetailPage");
const ExploreTemplateDetailPage = lazyNamed(loadExploreModule, "ExploreTemplateDetailPage");
const AppsPage = lazyNamed(loadStudioModule, "AppsPage");
const AgentChatPage = lazyNamed(loadStudioModule, "AgentChatPage");
const AssistantsPage = lazyNamed(loadStudioModule, "AssistantsPage");
const AiAssistantPage = lazyNamed(loadStudioModule, "AiAssistantPage");
const AppDetailPage = lazyNamed(loadStudioModule, "AppDetailPage");
const AppPublishPage = lazyNamed(loadStudioModule, "AppPublishPage");
const AssistantPublishPage = lazyNamed(loadStudioModule, "AssistantPublishPage");
const BotIdePage = lazyNamed(loadStudioModule, "BotIdePage");
const DataResourcesPage = lazyNamed(loadStudioModule, "DataResourcesPage");
const DashboardPage = lazyNamed(loadStudioModule, "DashboardPage");
const DatabaseDetailPage = lazyNamed(loadStudioModule, "DatabaseDetailPage");
const DatabasesPage = lazyNamed(loadStudioModule, "DatabasesPage");
const KnowledgeBasesPage = lazyNamed(loadStudioModule, "KnowledgeBasesPage");
const ModelConfigsPage = lazyNamed(loadStudioModule, "ModelConfigsPage");
const PluginDetailPage = lazyNamed(loadStudioModule, "PluginDetailPage");
const PluginsPage = lazyNamed(loadStudioModule, "PluginsPage");
const PublishCenterPage = lazyNamed(loadStudioModule, "PublishCenterPage");
const ResourceReferenceCard = lazyNamed(loadStudioModule, "ResourceReferenceCard");
const StudioWorkspacePage = lazyNamed(loadStudioModule, "StudioWorkspacePage");
const StudioContextProvider = lazyNamed(loadStudioModule, "StudioContextProvider");
const VariablesPage = lazyNamed(loadStudioModule, "VariablesPage");
const CozeWorkflowPage = lazyNamed(loadCozeWorkflowPlaygroundModule, "WorkflowPage");

function readLibraryMockFlag(): boolean {
  try {
    const env = (import.meta as ImportMeta & {
      env?: Record<string, string | undefined>;
    }).env;
    const raw = env?.VITE_LIBRARY_MOCK ?? env?.["VITE_LIBRARY_MOCK"];
    return typeof raw === "string" && raw.trim().toLowerCase() === "true";
  } catch {
    return false;
  }
}

const realLibraryApi: LibraryKnowledgeApi = {
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
  // v5 §32-44 扩展能力（在前端 mock 切到真实 API 时自动启用）
  listJobs: listKnowledgeJobs,
  listJobsAcrossKnowledgeBases: listAllKnowledgeJobs,
  getJob: getKnowledgeJob,
  rerunParseJob,
  rebuildIndex: rebuildKnowledgeIndex,
  retryDeadLetter: retryKnowledgeJob,
  cancelJob: cancelKnowledgeJob,
  listBindings: listKnowledgeBindings,
  createBinding: createKnowledgeBinding,
  removeBinding: removeKnowledgeBinding,
  listAllBindings: listAllKnowledgeBindings,
  listPermissions: listKnowledgePermissions,
  grantPermission: grantKnowledgePermission,
  revokePermission: revokeKnowledgePermission,
  listVersions: listKnowledgeVersions,
  createVersionSnapshot: createKnowledgeVersionSnapshot,
  releaseVersion: releaseKnowledgeVersion,
  rollbackToVersion: rollbackKnowledgeVersion,
  diffVersions: diffKnowledgeVersions,
  listProviderConfigs: listKnowledgeProviderConfigs,
  listTableColumns: listKnowledgeTableColumns,
  listTableRows: (kbId, docId, request) => listKnowledgeTableRows(kbId, docId, request),
  listImageItems: (kbId, docId, request) => listKnowledgeImageItems(kbId, docId, request),
  updateChunkingProfile: updateKnowledgeChunkingProfile,
  updateRetrievalProfile: updateKnowledgeRetrievalProfile,
  listRetrievalLogs: listKnowledgeRetrievalLogs,
  getRetrievalLog: getKnowledgeRetrievalLog,
  runRetrieval: runKnowledgeRetrieval,
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

// 知识库专题（v5 §32-44）阶段：当 VITE_LIBRARY_MOCK=true 时切到前端 mock 适配器，
// 让资源中心 / 上传与解析 / 切片与索引 / 检索与注入 / 治理与开放接口五个系统面
// 在后端尚未补齐前，已经能完整跑通 UI、状态机与异步任务。
// 后端按契约补齐后，将开关关闭即可恢复真实链路（见 docs/plan-knowledge-platform-v5.md）。
const libraryApi: LibraryKnowledgeApi = readLibraryMockFlag()
  ? createMockLibraryApi({ tickIntervalMs: 800, withFailures: true })
  : realLibraryApi;

function navGlyph(label: string) {
  return <span className="app-nav-glyph" aria-hidden="true">{label}</span>;
}

function LoadingPage() {
  return <PageShell loading />;
}

function useResolvedOrgId(): string {
  const params = useParams();
  const organization = useOptionalOrganizationContext();
  return params.orgId ?? organization?.orgId ?? getTenantId() ?? "";
}

function useResolvedWorkspaceId(): string {
  const params = useParams();
  const workspace = useOptionalWorkspaceContext();
  return params.workspaceId ?? workspace?.id ?? "";
}

function useResolvedAppKey(): string {
  const params = useParams();
  const workspace = useOptionalWorkspaceContext();
  return params.appKey ?? workspace?.appKey ?? getConfiguredAppKey() ?? "";
}

function UnauthorizedNavigationBridge() {
  const navigate = useNavigate();

  useEffect(() => {
    setUnauthorizedHandler(() => {
      navigate(signPath(), { replace: true });
    });

    return () => {
      setUnauthorizedHandler(null);
    };
  }, [navigate]);

  return null;
}

function ProtectedPage({ permission, children }: { permission?: string; children: ReactElement }) {
  const auth = useAuth();
  const appKey = useResolvedAppKey();
  if (permission && !auth.hasPermission(permission)) {
    return <Navigate to={appKey ? appForbiddenPath(appKey) : "/forbidden"} replace />;
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
          ? buildChatflowWorkbenchPath(appKey, workflowId)
          : buildWorkflowWorkbenchPath(appKey, workflowId)
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

  if (path.startsWith("/workflows/") || path.startsWith("/chatflows/")) {
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

export function createStudioApi(appKey: string): StudioModuleApi {
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
    // D5：批量插入入口（同步 + 异步）。
    bulkCreateDatabaseRecords: createAiDatabaseRecordsBulk,
    submitDatabaseBulkInsertJob: submitAiDatabaseBulkInsertJob,
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
    runWorkflowTask: async (workflowId, incident) => {
      const response = await requestApi<{
        success: boolean;
        message?: string;
        data?: {
          execution: {
            executionId: string;
            status?: string;
            outputsJson?: string;
            errorMessage?: string;
          };
          trace?: {
            executionId: string;
            status?: string;
            startedAt?: string;
            completedAt?: string;
            durationMs?: number;
            steps: Array<{
              nodeKey: string;
              status?: string;
              nodeType?: string;
              durationMs?: number;
              errorMessage?: string;
            }>;
          };
        };
      }>(`/workflow-playground/${encodeURIComponent(workflowId)}/execute`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify({
          incident,
          source: "draft"
        })
      });

      if (!response.success || !response.data) {
        throw new Error(response.message || "Failed to execute workflow task");
      }

      return response.data;
    },
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
    },
    // 治理 R1-F1：发布渠道接入 — 真实 REST 适配
    listWorkspacePublishChannels: (workspaceId: string) => listWorkspacePublishChannels(workspaceId),
    getWorkspaceChannelActiveRelease: (workspaceId: string, channelId: string) =>
      getWorkspaceChannelActiveRelease(workspaceId, channelId),
    httpJson: publishChannelsHttpJson
  };
}

export function useAppApis(appKey: string) {
  const { t } = useAppI18n();
  return useMemo(() => ({
    adminApi: createAdminApi(appKey),
    exploreApi: createExploreApi(appKey),
    studioApi: createStudioApi(appKey)
  }), [appKey, t]);
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
      onOpenWorkflow={workflowId => navigate(buildWorkflowWorkbenchPath(appKey, workflowId))}
    />
  );
}

function StudioLibraryAliasRedirect() {
  const { spaceId = "" } = useParams();
  const bootstrap = useBootstrap();
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  const effectiveSpaceId = spaceId || bootstrap.spaceId;
  const studioLibraryApi = useMemo<LibraryKnowledgeApi>(() => ({
    ...libraryApi,
    createKnowledgeBase: request => createKnowledgeBase({ ...request, workspaceId: Number(effectiveSpaceId) }),
    updateKnowledgeBase: (knowledgeBaseId, request) => updateKnowledgeBase(knowledgeBaseId, { ...request, workspaceId: Number(effectiveSpaceId) })
  }), [effectiveSpaceId]);
  return <LibraryPage api={studioLibraryApi} locale={locale} appKey={bootstrap.appKey} spaceId={effectiveSpaceId} onNavigate={navigate} />;
}

function StudioVariablesRoute() {
  const appKey = useResolvedAppKey();
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
  const { id = "" } = useParams();
  const orgId = useResolvedOrgId();
  const workspace = useWorkspaceContext();
  const appKey = useResolvedAppKey();
  const { locale } = useAppI18n();
  const navigate = useNavigate();
  const { studioApi } = useAppApis(appKey);
  return (
    <PluginDetailPage
      api={studioApi}
      locale={locale}
      pluginId={Number(id)}
      onOpenLibrary={() => navigate(orgWorkspaceLibraryPath(orgId, workspace.id))}
      onOpenExplore={() => navigate("/explore/plugin")}
    />
  );
}

function WorkspacePluginToolRoute() {
  const { id = "", toolId = "" } = useParams();
  const orgId = useResolvedOrgId();
  const workspace = useWorkspaceContext();
  const [searchParams] = useSearchParams();
  const targetParams = new URLSearchParams(searchParams);

  if (toolId) {
    targetParams.set("toolId", toolId);
  }

  const query = targetParams.toString();
  const targetPath = orgWorkspacePluginDetailPath(orgId, workspace.id, id);
  return <Navigate to={query ? `${targetPath}?${query}` : targetPath} replace />;
}

function StudioPublishCenterRoute() {
  const orgId = useResolvedOrgId();
  const workspace = useWorkspaceContext();
  const appKey = useResolvedAppKey();
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  const { studioApi } = useAppApis(appKey);
  return (
    <PublishCenterPage
      api={studioApi}
      locale={locale}
      apiBase={typeof window !== "undefined" ? `${window.location.origin}/api/v1` : "/api/v1"}
      // 治理 R1-F1：把当前 workspace.id 透传给 PublishCenterPage 启用「发布渠道」 Tab
      workspaceId={workspace.id}
      onOpenAgent={id => navigate(orgWorkspaceAgentDetailPath(orgId, workspace.id, id))}
      onOpenApp={id => navigate(orgWorkspaceAppDetailPath(orgId, workspace.id, id))}
      onOpenWorkflow={id => navigate(buildWorkspaceWorkbenchPath(orgId, workspace.id, "workflow", id))}
      onOpenPlugin={id => navigate(orgWorkspacePluginDetailPath(orgId, workspace.id, id))}
    />
  );
}

function StudioAssistantToolsRoute() {
  const appKey = useResolvedAppKey();
  const { locale } = useAppI18n();
  const { studioApi } = useAppApis(appKey);
  return <AiAssistantPage api={studioApi} locale={locale} />;
}

function StudioDataRoute() {
  const orgId = useResolvedOrgId();
  const workspace = useWorkspaceContext();
  const appKey = useResolvedAppKey();
  const { locale } = useAppI18n();
  const navigate = useNavigate();
  const { studioApi } = useAppApis(appKey);
  return (
    <DataResourcesPage
      api={studioApi}
      locale={locale}
      onOpenLibrary={() => navigate(orgWorkspaceLibraryPath(orgId, workspace.id))}
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
  const { id = "" } = useParams();
  const orgId = useResolvedOrgId();
  const workspace = useWorkspaceContext();
  const appKey = useResolvedAppKey();
  const { locale } = useAppI18n();
  const navigate = useNavigate();
  const { studioApi } = useAppApis(appKey);
  return (
    <DatabaseDetailPage
      api={studioApi}
      locale={locale}
      databaseId={Number(id)}
      onOpenLibrary={() => navigate(orgWorkspaceLibraryPath(orgId, workspace.id))}
    />
  );
}

function WorkflowsAliasRedirect() {
  const { appKey = "" } = useParams();
  return <Navigate to={workflowListPath(appKey)} replace />;
}

function buildWorkflowWorkbenchPath(appKey: string, workflowId?: string, contentMode: WorkflowWorkbenchContentMode = "canvas") {
  const basePath = workflowListPath(appKey);
  const pathname = contentMode === "canvas"
    ? basePath
    : `${basePath}/${contentMode === "session" ? "session" : "variables"}`;
  const params = new URLSearchParams();
  if (workflowId) {
    params.set("workflow_id", workflowId);
  }
  const query = params.toString();
  return query ? `${pathname}?${query}` : pathname;
}

function buildChatflowWorkbenchPath(appKey: string, workflowId?: string, contentMode: WorkflowWorkbenchContentMode = "canvas") {
  const basePath = `/apps/${encodeURIComponent(appKey)}/chatflows`;
  const pathname = contentMode === "canvas"
    ? basePath
    : `${basePath}/${contentMode === "session" ? "session" : "variables"}`;
  const params = new URLSearchParams();
  if (workflowId) {
    params.set("workflow_id", workflowId);
  }
  const query = params.toString();
  return query ? `${pathname}?${query}` : pathname;
}

const DEVELOP_FOCUS_VALUES: DevelopFocus[] = [
  "overview",
  "agents",
  "projects",
  "workflow",
  "chatflow",
  "plugins",
  "data",
  "models",
  "chat"
];

function resolveDevelopFocus(searchParams: URLSearchParams, fallback: DevelopFocus): DevelopFocus {
  const focus = searchParams.get("focus");
  return focus && DEVELOP_FOCUS_VALUES.includes(focus as DevelopFocus) ? focus as DevelopFocus : fallback;
}

function mapWorkflowStatusLabel(status: WorkflowListItem["status"], locale: AppLocale): string {
  if (status === 1) {
    return locale === "en-US" ? "Published" : "已发布";
  }

  return locale === "en-US" ? "Draft" : "草稿";
}

function toDevelopWorkflowSummary(item: WorkflowListItem, locale: AppLocale): DevelopResourceSummary {
  return {
    id: item.id,
    kind: item.mode === 1 ? "chatflow" : "workflow",
    title: item.name,
    description: item.description,
    status: mapWorkflowStatusLabel(item.status, locale),
    updatedAt: item.updatedAt,
    meta: typeof item.latestVersionNumber === "number"
      ? locale === "en-US"
        ? `Version ${item.latestVersionNumber}`
        : `版本 ${item.latestVersionNumber}`
      : undefined
  };
}

function useWorkspaceDevelopResources(locale: AppLocale, workspaceId: string) {
  const [workflowItems, setWorkflowItems] = useState<DevelopResourceSummary[]>([]);
  const [chatflowItems, setChatflowItems] = useState<DevelopResourceSummary[]>([]);

  useEffect(() => {
    let cancelled = false;

    async function loadWorkflowResources() {
      try {
        const response = await listWorkflows(1, 100, undefined, workspaceId);
        if (cancelled) {
          return;
        }

        const items = (response.data?.items ?? []).map(item => toDevelopWorkflowSummary(item, locale));
        setWorkflowItems(items.filter(item => item.kind === "workflow"));
        setChatflowItems(items.filter(item => item.kind === "chatflow"));
      } catch {
        if (!cancelled) {
          setWorkflowItems([]);
          setChatflowItems([]);
        }
      }
    }

    void loadWorkflowResources();
    return () => {
      cancelled = true;
    };
  }, [locale, workspaceId]);

  return { workflowItems, chatflowItems };
}

function WorkspaceStudioRoute({
  defaultFocus = "overview"
}: {
  defaultFocus?: DevelopFocus;
}) {
  const orgId = useResolvedOrgId();
  const workspace = useWorkspaceContext();
  const appKey = useResolvedAppKey();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { locale } = useAppI18n();
  const { studioApi } = useAppApis(appKey);
  const { workflowItems, chatflowItems } = useWorkspaceDevelopResources(locale, workspace.id);
  const focus = resolveDevelopFocus(searchParams, defaultFocus);

  return (
    <StudioWorkspacePage
      api={studioApi}
      locale={locale}
      focus={focus}
      workflowItems={workflowItems}
      chatflowItems={chatflowItems}
      onOpenBot={botId => navigate(orgWorkspaceAgentDetailPath(orgId, workspace.id, botId))}
      onOpenUsers={() => navigate(orgWorkspaceManagePath(orgId, workspace.id, "users"))}
      onOpenRoles={() => navigate(orgWorkspaceManagePath(orgId, workspace.id, "roles"))}
      onOpenDepartments={() => navigate(orgWorkspaceManagePath(orgId, workspace.id, "departments"))}
      onOpenPositions={() => navigate(orgWorkspaceManagePath(orgId, workspace.id, "positions"))}
      onOpenWorkflow={workflowId => navigate(buildWorkspaceWorkbenchPath(orgId, workspace.id, "workflow", workflowId))}
      onOpenChatflow={workflowId => navigate(buildWorkspaceWorkbenchPath(orgId, workspace.id, "chatflow", workflowId))}
      onOpenWorkflows={() => navigate(orgWorkspaceWorkflowsPath(orgId, workspace.id))}
      onOpenChatflows={() => navigate(orgWorkspaceChatflowsPath(orgId, workspace.id))}
      onOpenAgentChat={() => navigate(orgWorkspaceChatPath(orgId, workspace.id))}
      onOpenModelConfigs={() => navigate(orgWorkspaceModelConfigsPath(orgId, workspace.id))}
      onOpenLibrary={() => navigate(orgWorkspaceLibraryPath(orgId, workspace.id))}
      onOpenApplicationDetail={appId => navigate(orgWorkspaceAppDetailPath(orgId, workspace.id, appId))}
      onOpenApplicationPublish={appId => navigate(orgWorkspaceAppPublishPath(orgId, workspace.id, appId))}
      onCreateWorkflow={() => navigate(`${orgWorkspaceWorkflowsPath(orgId, workspace.id)}?create=1`)}
      onCreateChatflow={() => navigate(`${orgWorkspaceChatflowsPath(orgId, workspace.id)}?create=1`)}
    />
  );
}

function WorkflowEditorAliasRedirect() {
  const { appKey = "", id = "" } = useParams();
  return <Navigate to={buildWorkflowWorkbenchPath(appKey, id)} replace />;
}

function ChatflowsAliasRedirect() {
  const { appKey = "" } = useParams();
  return <Navigate to={`/apps/${encodeURIComponent(appKey)}/chatflows`} replace />;
}

function ChatflowEditorAliasRedirect() {
  const { appKey = "", id = "" } = useParams();
  return <Navigate to={buildChatflowWorkbenchPath(appKey, id)} replace />;
}

function AppShellRoute() {
  const { appKey = "" } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  const bootstrap = useBootstrap();
  const auth = useAuth();
  const { locale, setLocale, t } = useAppI18n();
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
  const navSections: CozeNavSection[] = [
    {
      key: "workspace",
      title: t("shellNavWorkspace"),
      items: [
        { key: "develop", label: t("shellNavDevelop"), icon: navGlyph("D"), path: workspaceDevelopPath(appKey, bootstrap.spaceId), testId: "app-sidebar-item-develop" },
        { key: "agents", label: t("sidebarAgents"), icon: navGlyph("A"), path: studioAssistantsPath(appKey), testId: "app-sidebar-item-agents" },
        { key: "workflow", label: t("sidebarWorkflow"), icon: navGlyph("W"), path: workflowListPath(appKey), testId: "app-sidebar-item-workflows" },
        { key: "library", label: t("sidebarLibrary"), icon: navGlyph("L"), path: studioLibraryPath(appKey), testId: "app-sidebar-item-library" },
        { key: "plugins", label: t("shellNavPlugins"), icon: navGlyph("PL"), path: studioPluginsPath(appKey), testId: "app-sidebar-item-plugins" },
        { key: "chat", label: t("sidebarChat"), icon: navGlyph("C"), path: workspaceChatPath(appKey, bootstrap.spaceId), testId: "app-sidebar-item-agent-chat" },
        { key: "model-configs", label: t("sidebarModels"), icon: navGlyph("M"), path: workspaceModelConfigsPath(appKey, bootstrap.spaceId), testId: "app-sidebar-item-model-configs" }
      ],
      overflowLabel: t("shellNavMore"),
      overflowTestId: "app-sidebar-section-more-workspace",
      overflowItems: [
        { key: "projects", label: t("shellNavApplications"), icon: navGlyph("APP"), path: studioAppsPath(appKey), testId: "app-sidebar-item-projects" },
        { key: "chatflow", label: t("shellNavChatflow"), icon: navGlyph("CF"), path: `/apps/${encodeURIComponent(appKey)}/chatflows`, testId: "app-sidebar-item-chatflows" },
        { key: "assistant", label: t("sidebarAssistant"), icon: navGlyph("AI"), path: studioAssistantToolsPath(appKey), testId: "app-sidebar-item-ai-assistant" },
        { key: "data", label: t("sidebarData"), icon: navGlyph("DT"), path: studioDataPath(appKey), testId: "app-sidebar-item-data" },
        { key: "knowledge-bases", label: t("sidebarKnowledge"), icon: navGlyph("KB"), path: studioKnowledgeBasesPath(appKey), testId: "app-sidebar-item-knowledge-bases" },
        { key: "databases", label: t("shellNavDatabases"), icon: navGlyph("DBX"), path: studioDatabasesPath(appKey), testId: "app-sidebar-item-databases" },
        { key: "variables", label: t("shellNavVariables"), icon: navGlyph("VAR"), path: studioVariablesPath(appKey), testId: "app-sidebar-item-variables" }
      ]
    },
    {
      key: "explore",
      title: t("shellNavExplore"),
      items: [
        { key: "plugin", label: t("shellNavPluginStore"), icon: navGlyph("PL"), path: explorePath(appKey, "plugin"), testId: "app-sidebar-item-explore-plugins" },
        { key: "template", label: t("shellNavTemplateStore"), icon: navGlyph("TP"), path: explorePath(appKey, "template"), testId: "app-sidebar-item-explore-templates" }
      ]
    },
    {
      key: "admin",
      title: t("shellNavManagement"),
      items: [
        { key: "overview", label: t("sidebarOverview"), icon: navGlyph("OV"), path: adminPath(appKey, "overview"), testId: "app-sidebar-item-organization-overview" },
        { key: "users", label: t("sidebarUsers"), icon: navGlyph("U"), path: adminPath(appKey, "users"), testId: "app-sidebar-item-users" },
        { key: "roles", label: t("sidebarRoles"), icon: navGlyph("R"), path: adminPath(appKey, "roles"), testId: "app-sidebar-item-roles" },
        { key: "departments", label: t("sidebarDepartments"), icon: navGlyph("DP"), path: adminPath(appKey, "departments"), testId: "app-sidebar-item-departments" },
        { key: "positions", label: t("sidebarPositions"), icon: navGlyph("P"), path: adminPath(appKey, "positions"), testId: "app-sidebar-item-positions" }
      ],
      overflowLabel: t("shellNavMore"),
      overflowTestId: "app-sidebar-section-more-admin",
      overflowItems: [
        { key: "approval", label: t("shellNavApproval"), icon: navGlyph("AP"), path: adminPath(appKey, "approval"), testId: "app-sidebar-item-approval" },
        { key: "reports", label: t("shellNavReports"), icon: navGlyph("RP"), path: adminPath(appKey, "reports"), testId: "app-sidebar-item-reports" },
        { key: "dashboards", label: t("shellNavDashboards"), icon: navGlyph("DB"), path: adminPath(appKey, "dashboards"), testId: "app-sidebar-item-dashboards" },
        { key: "visualization", label: t("shellNavVisualization"), icon: navGlyph("VM"), path: adminPath(appKey, "visualization"), testId: "app-sidebar-item-visualization" },
        { key: "settings", label: t("sidebarSettings"), icon: navGlyph("S"), path: adminPath(appKey, "settings"), testId: "app-sidebar-item-settings" },
        { key: "profile", label: t("sidebarProfile"), icon: navGlyph("ME"), path: adminPath(appKey, "profile"), testId: "app-sidebar-item-profile" }
      ]
    }
  ];

  const activeNavItem = navSections
    .flatMap(section => [...section.items, ...(section.overflowItems ?? [])])
    .find(item => activeShellPath === item.path || activeShellPath.startsWith(`${item.path}/`) || activeShellPath.includes(item.path));

  const primaryKey = activeNavItem?.key ?? "workspace";
  const workspaceLabel = bootstrap.workspaceLabel || appKey;
  const withWorkspace = (key: Parameters<typeof t>[0]) => t(key).replace("{workspace}", workspaceLabel);

  const shellHeaderCopyMap: Record<string, { title: string; subtitle: string }> = {
    develop: { title: t("shellHeaderDevelopTitle"), subtitle: withWorkspace("shellHeaderDevelopSubtitle") },
    agents: { title: t("shellHeaderAgentsTitle"), subtitle: withWorkspace("shellHeaderAgentsSubtitle") },
    projects: { title: t("shellHeaderProjectsTitle"), subtitle: withWorkspace("shellHeaderProjectsSubtitle") },
    workflow: { title: t("shellHeaderWorkflowTitle"), subtitle: withWorkspace("shellHeaderWorkflowSubtitle") },
    chatflow: { title: t("shellHeaderChatflowTitle"), subtitle: withWorkspace("shellHeaderChatflowSubtitle") },
    library: { title: t("shellHeaderLibraryTitle"), subtitle: withWorkspace("shellHeaderLibrarySubtitle") },
    plugins: { title: t("shellHeaderPluginsTitle"), subtitle: withWorkspace("shellHeaderPluginsSubtitle") },
    chat: { title: t("shellHeaderChatTitle"), subtitle: withWorkspace("shellHeaderChatSubtitle") },
    "model-configs": { title: t("shellHeaderModelConfigsTitle"), subtitle: withWorkspace("shellHeaderModelConfigsSubtitle") },
    assistant: { title: t("shellHeaderAssistantTitle"), subtitle: withWorkspace("shellHeaderAssistantSubtitle") },
    data: { title: t("shellHeaderDataTitle"), subtitle: withWorkspace("shellHeaderDataSubtitle") },
    "knowledge-bases": { title: t("shellHeaderKnowledgeBasesTitle"), subtitle: withWorkspace("shellHeaderKnowledgeBasesSubtitle") },
    databases: { title: t("shellHeaderDatabasesTitle"), subtitle: withWorkspace("shellHeaderDatabasesSubtitle") },
    variables: { title: t("shellHeaderVariablesTitle"), subtitle: withWorkspace("shellHeaderVariablesSubtitle") },
    overview: { title: t("shellHeaderOverviewTitle"), subtitle: withWorkspace("shellHeaderOverviewSubtitle") },
    users: { title: t("shellHeaderUsersTitle"), subtitle: withWorkspace("shellHeaderUsersSubtitle") },
    roles: { title: t("shellHeaderRolesTitle"), subtitle: withWorkspace("shellHeaderRolesSubtitle") },
    departments: { title: t("shellHeaderDepartmentsTitle"), subtitle: withWorkspace("shellHeaderDepartmentsSubtitle") },
    positions: { title: t("shellHeaderPositionsTitle"), subtitle: withWorkspace("shellHeaderPositionsSubtitle") },
    approval: { title: t("shellHeaderApprovalTitle"), subtitle: withWorkspace("shellHeaderApprovalSubtitle") },
    reports: { title: t("shellHeaderReportsTitle"), subtitle: withWorkspace("shellHeaderReportsSubtitle") },
    dashboards: { title: t("shellHeaderDashboardsTitle"), subtitle: withWorkspace("shellHeaderDashboardsSubtitle") },
    visualization: { title: t("shellHeaderVisualizationTitle"), subtitle: withWorkspace("shellHeaderVisualizationSubtitle") },
    settings: { title: t("shellHeaderSettingsTitle"), subtitle: withWorkspace("shellHeaderSettingsSubtitle") },
    profile: { title: t("shellHeaderProfileTitle"), subtitle: withWorkspace("shellHeaderProfileSubtitle") },
    plugin: { title: t("shellHeaderPluginTitle"), subtitle: withWorkspace("shellHeaderPluginSubtitle") },
    template: { title: t("shellHeaderTemplateTitle"), subtitle: withWorkspace("shellHeaderTemplateSubtitle") }
  };

  const defaultHeaderCopy = primaryKey === "workspace"
    ? {
        title: t("shellHeaderWorkspaceTitle"),
        subtitle: withWorkspace("shellHeaderWorkspaceSubtitle")
      }
    : primaryKey === "explore"
      ? {
          title: t("shellHeaderExploreTitle"),
          subtitle: t("shellHeaderExploreSubtitle")
        }
      : {
          title: t("shellHeaderManagementTitle"),
          subtitle: t("shellHeaderManagementSubtitle")
        };

  const shellHeaderCopy = shellHeaderCopyMap[primaryKey] ?? defaultHeaderCopy;

  const isWorkflowWorkbenchRoute =
    location.pathname.includes("/workflows") ||
    location.pathname.includes("/chatflows");

  if (isWorkflowWorkbenchRoute) {
    return (
      <>
        <UnauthorizedNavigationBridge />
        <StudioContextProvider api={studioApi}>
          <Outlet />
        </StudioContextProvider>
      </>
    );
  }

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
        localeLabel={t(locale === "zh-CN" ? "switchToEnglish" : "switchToChinese")}
        userName={auth.profile?.displayName || auth.profile?.username || "Atlas"}
        profileLabel={t("sidebarProfile")}
        logoutLabel={t("logout")}
        sidebarTop={<WorkspaceSwitcher workspaceId={bootstrap.spaceId} workspaceLabel={bootstrap.workspaceLabel || appKey} />}
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
  const bootstrap = useBootstrap();
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  const routeLibraryApi = useMemo<LibraryKnowledgeApi>(() => ({
    ...libraryApi,
    createKnowledgeBase: request => createKnowledgeBase({ ...request, workspaceId: Number(bootstrap.spaceId) }),
    updateKnowledgeBase: (knowledgeBaseId, request) => updateKnowledgeBase(knowledgeBaseId, { ...request, workspaceId: Number(bootstrap.spaceId) })
  }), [bootstrap.spaceId]);
  return <LibraryPage api={routeLibraryApi} locale={locale} appKey={bootstrap.appKey} spaceId={bootstrap.spaceId} onNavigate={navigate} />;
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
  return <WorkspaceStudioRoute defaultFocus="overview" />;
}

function LegacyOrgWorkspaceHomeRedirect() {
  const orgId = useResolvedOrgId();
  const workspaceId = useResolvedWorkspaceId();
  return <Navigate to={orgWorkspaceHomePath(orgId, workspaceId)} replace />;
}

function LegacyWorkspaceHomeRedirect() {
  const orgId = useResolvedOrgId();
  const workspaceId = useResolvedWorkspaceId();
  return <Navigate to={orgWorkspaceHomePath(orgId, workspaceId)} replace />;
}

function DashboardRoute() {
  const orgId = useResolvedOrgId();
  const workspace = useWorkspaceContext();
  if (!workspace.appKey) {
    return <WorkspaceNoAppDashboard />;
  }
  const appKey = useResolvedAppKey();
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  const { studioApi } = useAppApis(appKey);
  return (
    <DashboardPage
      api={studioApi}
      locale={locale}
      onNavigateToResource={(type, id) => {
        if (type === "agent") navigate(orgWorkspaceAgentDetailPath(orgId, workspace.id, id));
        else if (type === "app") navigate(orgWorkspaceAppDetailPath(orgId, workspace.id, id));
        else if (type === "workflow") navigate(buildWorkspaceWorkbenchPath(orgId, workspace.id, "workflow", id));
        else if (type === "plugin") navigate(orgWorkspacePluginDetailPath(orgId, workspace.id, id));
      }}
      onNavigateToModels={() => navigate(orgWorkspaceModelConfigsPath(orgId, workspace.id))}
      onNavigateToPublish={() => navigate(orgWorkspacePublishCenterPath(orgId, workspace.id))}
      onCreateAgent={() => navigate(`${orgWorkspaceDevelopPath(orgId, workspace.id)}?focus=agents`)}
      onCreateApp={() => navigate(`${orgWorkspaceDevelopPath(orgId, workspace.id)}?focus=projects`)}
      onCreateWorkflow={() => navigate(`${orgWorkspaceWorkflowsPath(orgId, workspace.id)}?create=1`)}
    />
  );
}

function WorkspaceNoAppDashboard() {
  const orgId = useResolvedOrgId();
  const workspace = useWorkspaceContext();
  const navigate = useNavigate();
  const { t } = useAppI18n();
  const [createOpen, setCreateOpen] = useState(false);

  return (
    <>
      <div data-testid="workspace-no-app-dashboard">
        <ResultCard
          status="info"
          title={t("workspaceNoAppTitle")}
          description={t("workspaceNoAppDescription").replace("{workspace}", workspace.name || workspace.id)}
          actions={
            <>
              <Button
                type="primary"
                theme="solid"
                data-testid="workspace-no-app-create"
                onClick={() => setCreateOpen(true)}
              >
                {t("workspaceNoAppCreate")}
              </Button>
              <Button
                type="tertiary"
                theme="light"
                data-testid="workspace-no-app-back"
                onClick={() => navigate(orgWorkspacesPath(orgId))}
              >
                {t("workspaceNoAppBack")}
              </Button>
            </>
          }
        />
      </div>
      <GlobalCreateModal
        visible={createOpen}
        workspaceId={workspace.id}
        onClose={() => setCreateOpen(false)}
      />
    </>
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
  const appKey = useResolvedAppKey();
  const { locale } = useAppI18n();
  const { studioApi } = useAppApis(appKey);
  return <AgentChatPage api={studioApi} locale={locale} />;
}

function AiAssistantRoute() {
  const appKey = useResolvedAppKey();
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
      onOpenWorkflow={workflowId => navigate(buildWorkflowWorkbenchPath(appKey, workflowId))}
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
  const appKey = useResolvedAppKey();
  const { locale } = useAppI18n();
  const { studioApi } = useAppApis(appKey);
  return <ModelConfigsPage api={studioApi} locale={locale} />;
}

function WorkspaceWorkflowWorkbenchRoute({
  mode = "workflow"
}: {
  mode?: WorkflowResourceMode;
}) {
  const params = useParams();
  const [searchParams] = useSearchParams();
  const orgId = useResolvedOrgId();
  const navigate = useNavigate();
  const workspace = useWorkspaceContext();
  const { t } = useAppI18n();
  const [creationError, setCreationError] = useState("");
  const localizeCreationError = useCallback((error: Error | null) => {
    const fallback = mode === "chatflow" ? t("studioCreateChatflowFailed") : t("studioCreateWorkflowFailed");
    if (!error) {
      return fallback;
    }

    const message = error.message.trim();
    if (!message || /^Request failed with status code \d+$/i.test(message) || /^Network Error$/i.test(message)) {
      return fallback;
    }

    return `${fallback} ${message}`;
  }, [mode, t]);
  const selectedWorkflowId =
    params.workflowId ??
    params.id ??
    searchParams.get("workflow_id") ??
    searchParams.get("workflowId") ??
    "";
  const shouldCreate = searchParams.get("create") === "1";
  const returnUrl = searchParams.get("return_url") ?? searchParams.get("returnUrl") ?? undefined;

  useEffect(() => {
    if (selectedWorkflowId || !shouldCreate) {
      return;
    }

    let cancelled = false;
    setCreationError("");

    async function createAndRedirect() {
      try {
        const result = await createWorkflowDefinition({
          name: mode === "chatflow" ? t("studioCreateChatflowDefaultName") : t("studioCreateWorkflowDefaultName"),
          mode: mode === "chatflow" ? 1 : 0,
          workspaceId: workspace.id
        });
        const workflowId = result.data ?? "";

        if (!result.success || !workflowId) {
          throw new Error(result.message || (mode === "chatflow" ? t("studioCreateChatflowFailed") : t("studioCreateWorkflowFailed")));
        }

        if (!cancelled) {
          navigate(buildWorkspaceWorkbenchPath(orgId, workspace.id, mode, workflowId), { replace: true });
        }
      } catch (error) {
        if (!cancelled) {
          setCreationError(localizeCreationError(error instanceof Error ? error : null));
        }
      }
    }

    void createAndRedirect();
    return () => {
      cancelled = true;
    };
  }, [localizeCreationError, mode, navigate, orgId, selectedWorkflowId, shouldCreate, t, workspace.id]);

  if (!selectedWorkflowId) {
    if (shouldCreate) {
      return (
        <PageShell centered maxWidth={520} testId={`workspace-${mode}-create`}>
          <Card bodyStyle={{ padding: 32 }}>
            <Title heading={3} style={{ margin: 0 }}>
              {creationError
                ? mode === "chatflow"
                  ? t("studioCreateChatflowFailed")
                  : t("studioCreateWorkflowFailed")
                : mode === "chatflow"
                  ? t("studioCreateChatflowPending")
                  : t("studioCreateWorkflowPending")}
            </Title>
            <Text type="tertiary" style={{ display: "block", marginTop: 8 }}>
              {creationError || t("workflowRuntimePreparing")}
            </Text>
          </Card>
        </PageShell>
      );
    }

    return <WorkspaceStudioRoute defaultFocus={mode} />;
  }

  return (
    <WorkflowRuntimeBoundary>
      <CozeWorkflowPage
        workflowId={selectedWorkflowId}
        mode={mode}
        spaceId={workspace.id}
        returnUrl={returnUrl}
        onAtlasBack={() => {
          if (returnUrl && typeof window !== "undefined") {
            window.location.assign(returnUrl);
            return;
          }
          if (typeof window !== "undefined" && window.history.length > 1) {
            navigate(-1);
            return;
          }
          navigate("/", { replace: true });
        }}
      />
    </WorkflowRuntimeBoundary>
  );
}

function AdminUsersRoute() {
  const appKey = useResolvedAppKey();
  const { locale } = useAppI18n();
  const { adminApi } = useAppApis(appKey);
  return <UsersAdminPage api={adminApi} locale={locale} />;
}

function AdminOverviewRoute() {
  const appKey = useResolvedAppKey();
  const { locale } = useAppI18n();
  const { adminApi } = useAppApis(appKey);
  return <OrganizationOverviewPage api={adminApi} locale={locale} />;
}

function AdminRolesRoute() {
  const appKey = useResolvedAppKey();
  const { locale } = useAppI18n();
  const { adminApi } = useAppApis(appKey);
  return <RolesAdminPage api={adminApi} locale={locale} />;
}

function AdminDepartmentsRoute() {
  const appKey = useResolvedAppKey();
  const { locale } = useAppI18n();
  const { adminApi } = useAppApis(appKey);
  return <DepartmentsAdminPage api={adminApi} locale={locale} />;
}

function AdminPositionsRoute() {
  const appKey = useResolvedAppKey();
  const { locale } = useAppI18n();
  const { adminApi } = useAppApis(appKey);
  return <PositionsAdminPage api={adminApi} locale={locale} />;
}

function AdminApprovalRoute() {
  const appKey = useResolvedAppKey();
  const { locale } = useAppI18n();
  const { adminApi } = useAppApis(appKey);
  return <ApprovalAdminPage api={adminApi} locale={locale} />;
}

function AdminReportsRoute() {
  const appKey = useResolvedAppKey();
  const { locale } = useAppI18n();
  const { adminApi } = useAppApis(appKey);
  return <ReportsAdminPage api={adminApi} locale={locale} />;
}

function AdminDashboardsRoute() {
  const appKey = useResolvedAppKey();
  const { locale } = useAppI18n();
  const { adminApi } = useAppApis(appKey);
  return <DashboardsAdminPage api={adminApi} locale={locale} />;
}

function AdminVisualizationRoute() {
  const appKey = useResolvedAppKey();
  const { locale } = useAppI18n();
  const { adminApi } = useAppApis(appKey);
  return <VisualizationAdminPage api={adminApi} locale={locale} />;
}

function AdminSettingsRoute() {
  const appKey = useResolvedAppKey();
  const { locale } = useAppI18n();
  const { adminApi } = useAppApis(appKey);
  return <SettingsAdminPage api={adminApi} locale={locale} />;
}

function AdminProfileRoute() {
  const appKey = useResolvedAppKey();
  const { locale } = useAppI18n();
  const { adminApi } = useAppApis(appKey);
  return <ProfileAdminPage api={adminApi} locale={locale} />;
}

function ExplorePluginsRoute() {
  const appKey = useResolvedAppKey();
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
  const appKey = useResolvedAppKey();
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
  const { word = "" } = useParams();
  const appKey = useResolvedAppKey();
  const { locale } = useAppI18n();
  const { exploreApi } = useAppApis(appKey);
  const navigate = useNavigate();
  return <ExploreSearchPage api={exploreApi} locale={locale} keyword={decodeURIComponent(word)} onOpenLocal={path => navigate(normalizeExploreLocalPath(appKey, path))} />;
}

function ExplorePluginDetailRoute() {
  const { productId = "" } = useParams();
  const appKey = useResolvedAppKey();
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
  const { templateId = "" } = useParams();
  const appKey = useResolvedAppKey();
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

function RootEntryRoute() {
  const auth = useAuth();
  const bootstrap = useBootstrap();
  const tenantId = getTenantId() ?? "";

  if (bootstrap.loading || auth.loading) {
    return <LoadingPage />;
  }

  if (!bootstrap.platformReady) {
    return <Navigate to="/platform-not-ready" replace />;
  }

  if (!bootstrap.appReady) {
    return <Navigate to="/app-setup" replace />;
  }

  if (!auth.isAuthenticated || !tenantId) {
    return <Navigate to={signPath()} replace />;
  }

  return <Navigate to={orgWorkspacesPath(tenantId)} replace />;
}

function WorkspaceListRoute() {
  const { orgId = "" } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const auth = useAuth();
  const bootstrap = useBootstrap();
  const { locale, setLocale, t } = useAppI18n();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [deletingWorkspaceId, setDeletingWorkspaceId] = useState<string | null>(null);
  const [keyword, setKeyword] = useState("");
  const [items, setItems] = useState<Awaited<ReturnType<typeof getWorkspaces>>>([]);
  const [selectedWorkspaceId, setSelectedWorkspaceId] = useState("");

  const loadWorkspaces = async () => {
    if (!auth.isAuthenticated) {
      setItems([]);
      setLoading(false);
      return;
    }

    setLoading(true);
    try {
      const result = await getWorkspaces(orgId);
      setItems(result);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      if (!auth.isAuthenticated) {
        setLoading(false);
        return;
      }
        setLoading(true);
        try {
          const result = await getWorkspaces(orgId);
          if (!cancelled) {
            setItems(result);
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    void load();
    return () => {
      cancelled = true;
    };
  }, [auth.isAuthenticated, orgId]);

  useEffect(() => {
    if (items.length === 0) {
      if (selectedWorkspaceId) {
        setSelectedWorkspaceId("");
      }
      return;
    }

    const rememberedWorkspaceId = readLastWorkspaceId();
    const fallbackWorkspaceId =
      (rememberedWorkspaceId && items.some(item => item.id === rememberedWorkspaceId) ? rememberedWorkspaceId : null) ??
      items[0]?.id ??
      "";

    setSelectedWorkspaceId(current =>
      current && items.some(item => item.id === current) ? current : fallbackWorkspaceId
    );
  }, [items, selectedWorkspaceId]);

  if (bootstrap.loading || auth.loading) {
    return <LoadingPage />;
  }

  if (!auth.isAuthenticated) {
    return <Navigate to={signPath()} replace />;
  }

  const activePath = `${location.pathname}${location.search}`;
  const currentWorkspace = items.find(item => item.id === selectedWorkspaceId) ?? items[0] ?? null;
  const workspaceId = currentWorkspace?.id ?? "";
  const workspaceLabel = currentWorkspace?.name || currentWorkspace?.appKey || t("cozeShellWorkspaceSwitcherTitle");

  if (!loading && workspaceId) {
    return <Navigate to={orgWorkspaceHomePath(orgId, workspaceId)} replace />;
  }

  const workspaceRoute = (resolver: (resolvedOrgId: string, resolvedWorkspaceId: string) => string) => {
    if (!workspaceId) {
      return orgWorkspacesPath(orgId);
    }
    return resolver(orgId, workspaceId);
  };

  const navSections: CozeNavSection[] = [
    {
      key: "workspace",
      title: t("cozeMenuGroupWorkspace"),
      items: [
        {
          key: "home",
          label: t("cozeMenuHome"),
          icon: navGlyph("首"),
          path: workspaceRoute(orgWorkspaceHomePath),
          testId: "app-sidebar-item-home"
        },
        {
          key: "projects",
          label: t("cozeMenuProjects"),
          icon: navGlyph("项"),
          path: workspaceRoute(orgWorkspaceDevelopPath),
          testId: "app-sidebar-item-projects"
        },
        {
          key: "resources",
          label: t("cozeMenuResources"),
          icon: navGlyph("资"),
          path: workspaceRoute(orgWorkspaceLibraryPath),
          testId: "app-sidebar-item-resources"
        },
        {
          key: "tasks",
          label: t("cozeMenuTasks"),
          icon: navGlyph("任"),
          path: workspaceRoute(orgWorkspaceManagePath),
          testId: "app-sidebar-item-tasks"
        },
        {
          key: "evaluations",
          label: t("cozeMenuEvaluations"),
          icon: navGlyph("评"),
          path: workspaceRoute(orgWorkspacePublishCenterPath),
          testId: "app-sidebar-item-evaluations"
        },
        {
          key: "settings",
          label: t("cozeMenuSettings"),
          icon: navGlyph("配"),
          path: workspaceRoute(orgWorkspaceSettingsPath),
          testId: "app-sidebar-item-settings"
        }
      ]
    },
    {
      key: "ecosystem",
      title: t("cozeMenuGroupEcosystem"),
      items: [
        {
          key: "templates",
          label: t("cozeMenuTemplates"),
          icon: navGlyph("模"),
          path: marketTemplatesPath(),
          testId: "app-sidebar-item-templates"
        },
        {
          key: "plugins",
          label: t("cozeMenuPlugins"),
          icon: navGlyph("插"),
          path: marketPluginsPath(),
          testId: "app-sidebar-item-plugins"
        },
        {
          key: "community",
          label: t("cozeMenuCommunity"),
          icon: navGlyph("社"),
          path: communityWorksPath(),
          testId: "app-sidebar-item-community"
        },
        {
          key: "open-api",
          label: t("cozeMenuOpenApi"),
          icon: navGlyph("API"),
          path: openApiPath(),
          testId: "app-sidebar-item-open-api"
        }
      ]
    }
  ];

  return (
    <CozeShell
      appKey={currentWorkspace?.appKey || "atlas"}
      workspaceLabel={workspaceLabel}
      activePath={activePath}
      navSections={navSections}
      headerTitle={t("workspaceListTitle")}
      headerSubtitle={workspaceLabel}
      localeLabel={t(locale === "zh-CN" ? "switchToEnglish" : "switchToChinese")}
      userName={auth.profile?.displayName || auth.profile?.username || "Atlas"}
      profileLabel={t("cozeShellAvatarMenuProfile")}
      logoutLabel={t("cozeShellAvatarMenuLogout")}
      sidebarTop={
        <WorkspaceSwitcher
          workspaceId={workspaceId}
          workspaceLabel={workspaceLabel}
          onSelectWorkspace={(targetWorkspaceId) => {
            setSelectedWorkspaceId(targetWorkspaceId);
            rememberLastWorkspaceId(targetWorkspaceId);
          }}
        />
      }
      onNavigate={path => navigate(path)}
      onToggleLocale={() => setLocale(locale === "zh-CN" ? "en-US" : "zh-CN")}
      onOpenProfile={() => navigate(meProfilePath())}
      onLogout={() => {
        void auth.logout().then(() => navigate(signPath(), { replace: true }));
      }}
    >
      <OrganizationProvider orgId={orgId}>
        <OrganizationWorkspacesPage
          loading={loading}
          canManage={auth.hasPermission(APP_PERMISSIONS.APPS_UPDATE)}
          saving={saving}
          deletingWorkspaceId={deletingWorkspaceId}
          keyword={keyword}
          items={items}
          activeWorkspaceId={workspaceId}
          activeWorkspaceLabel={workspaceLabel}
          onKeywordChange={setKeyword}
          onOpenWorkspace={targetWorkspaceId => navigate(orgWorkspaceHomePath(orgId, targetWorkspaceId))}
          onCreateWorkspace={async (request: WorkspaceCreateRequest) => {
            setSaving(true);
            try {
              const targetWorkspaceId = await createWorkspace(orgId, request);
              await loadWorkspaces();
              navigate(orgWorkspaceHomePath(orgId, targetWorkspaceId), { replace: true });
              return targetWorkspaceId;
            } finally {
              setSaving(false);
            }
          }}
          onUpdateWorkspace={async (targetWorkspaceId: string, request: WorkspaceUpdateRequest) => {
            setSaving(true);
            try {
              await updateWorkspace(orgId, targetWorkspaceId, request);
              await loadWorkspaces();
            } finally {
              setSaving(false);
            }
          }}
          onDeleteWorkspace={async (targetWorkspaceId: string) => {
            setDeletingWorkspaceId(targetWorkspaceId);
            try {
              await deleteWorkspace(orgId, targetWorkspaceId);
              await loadWorkspaces();
            } finally {
              setDeletingWorkspaceId(null);
            }
          }}
          onCreateAppInstance={async (targetWorkspaceId, request) => {
            const result = await createWorkspaceAppInstance(orgId, targetWorkspaceId, request);
            await loadWorkspaces();
            return result;
          }}
        />
      </OrganizationProvider>
    </CozeShell>
  );
}

function WorkspaceShellRoute() {
  const { orgId = "", workspaceId = "" } = useParams();

  return (
    <OrganizationProvider orgId={orgId}>
      <WorkspaceProvider orgId={orgId} workspaceId={workspaceId}>
        <WorkspaceShellInner />
      </WorkspaceProvider>
    </OrganizationProvider>
  );
}

function WorkspaceShellInner() {
  const location = useLocation();
  const navigate = useNavigate();
  const auth = useAuth();
  const bootstrap = useBootstrap();
  const { locale, setLocale, t } = useAppI18n();
  const organization = useResolvedOrgId();
  const workspace = useWorkspaceContext();
  const appKey = workspace.appKey;
  const { studioApi } = useAppApis(appKey);

  useEffect(() => {
    if (!workspace.loading && appKey) {
      rememberConfiguredAppKey(appKey);
      setAppInstanceIdToStorage(appKey, workspace.appInstanceId || null);
    }
  }, [appKey, workspace.appInstanceId, workspace.loading]);

  useEffect(() => {
    if (auth.isAuthenticated && !auth.profile && !auth.loading) {
      void auth.ensureProfile();
    }
  }, [auth]);

  if (bootstrap.loading || auth.loading || workspace.loading) {
    return <LoadingPage />;
  }

  const workspaceHomeRoute = orgWorkspaceHomePath(organization, workspace.id);

  if (!appKey) {
    if (location.pathname !== workspaceHomeRoute) {
      return <Navigate to={workspaceHomeRoute} replace />;
    }
  }

  if (!bootstrap.platformReady) {
    return <Navigate to="/platform-not-ready" replace />;
  }

  if (!bootstrap.appReady) {
    return <Navigate to="/app-setup" replace />;
  }

  if (!auth.isAuthenticated) {
    return <Navigate to={signPath(location.pathname + location.search)} replace />;
  }

  const activePath = `${location.pathname}${location.search}`;
  const navSections: CozeNavSection[] = [
    {
      key: "workspace",
      title: t("cozeMenuGroupWorkspace"),
      items: [
        {
          key: "home",
          label: t("cozeMenuHome"),
          icon: navGlyph("首"),
          path: workspaceHomeRoute,
          testId: "app-sidebar-item-home"
        },
        {
          key: "projects",
          label: t("cozeMenuProjects"),
          icon: navGlyph("项"),
          path: orgWorkspaceDevelopPath(organization, workspace.id),
          testId: "app-sidebar-item-projects"
        },
        {
          key: "resources",
          label: t("cozeMenuResources"),
          icon: navGlyph("资"),
          path: orgWorkspaceLibraryPath(organization, workspace.id),
          testId: "app-sidebar-item-resources"
        },
        {
          key: "tasks",
          label: t("cozeMenuTasks"),
          icon: navGlyph("任"),
          path: orgWorkspaceManagePath(organization, workspace.id),
          testId: "app-sidebar-item-tasks"
        },
        {
          key: "evaluations",
          label: t("cozeMenuEvaluations"),
          icon: navGlyph("评"),
          path: orgWorkspacePublishCenterPath(organization, workspace.id),
          testId: "app-sidebar-item-evaluations"
        },
        {
          key: "settings",
          label: t("cozeMenuSettings"),
          icon: navGlyph("配"),
          path: orgWorkspaceSettingsPath(organization, workspace.id),
          testId: "app-sidebar-item-settings"
        }
      ]
    },
    {
      key: "ecosystem",
      title: t("cozeMenuGroupEcosystem"),
      items: [
        {
          key: "templates",
          label: t("cozeMenuTemplates"),
          icon: navGlyph("模"),
          path: marketTemplatesPath(),
          testId: "app-sidebar-item-templates"
        },
        {
          key: "plugins",
          label: t("cozeMenuPlugins"),
          icon: navGlyph("插"),
          path: marketPluginsPath(),
          testId: "app-sidebar-item-plugins"
        },
        {
          key: "community",
          label: t("cozeMenuCommunity"),
          icon: navGlyph("社"),
          path: communityWorksPath(),
          testId: "app-sidebar-item-community"
        },
        {
          key: "open-api",
          label: t("cozeMenuOpenApi"),
          icon: navGlyph("API"),
          path: openApiPath(),
          testId: "app-sidebar-item-open-api"
        }
      ]
    }
  ];

  let headerTitle = t("shellNavDevelop");
  if (location.pathname.includes("/dashboard")) {
    headerTitle = t("shellNavDashboard");
  } else if (location.pathname.includes("/develop/chat")) {
    headerTitle = t("sidebarChat");
  } else if (location.pathname.includes("/develop/model-configs")) {
    headerTitle = t("sidebarModels");
  } else if (location.pathname.includes("/develop/assistant-tools")) {
    headerTitle = t("sidebarAssistant");
  } else if (location.pathname.includes("/develop/publish-center")) {
    headerTitle = t("shellHeaderPublishCenterTitle");
  } else if (location.pathname.includes("/library/data")) {
    headerTitle = t("sidebarData");
  } else if (location.pathname.includes("/library/variables")) {
    headerTitle = t("shellNavVariables");
  } else if (location.pathname.includes("/library")) {
    headerTitle = t("sidebarLibrary");
  } else if (location.pathname.includes("/manage")) {
    headerTitle = t("shellHeaderManagementTitle");
  } else if (location.pathname.includes("/settings/profile")) {
    headerTitle = t("sidebarProfile");
  } else if (location.pathname.includes("/settings/system")) {
    headerTitle = t("shellHeaderSystemSettingsTitle");
  } else if (location.pathname.includes("/settings")) {
    headerTitle = t("sidebarSettings");
  }
  const headerSubtitle = workspace.name || workspace.appKey;

  return (
    <>
      <UnauthorizedNavigationBridge />
      <CozeShell
        appKey={workspace.appKey}
        backPath={orgWorkspacesPath(organization)}
        workspaceLabel={workspace.name || workspace.appKey}
        activePath={activePath}
        navSections={navSections}
        headerTitle={headerTitle}
        headerSubtitle={headerSubtitle}
        localeLabel={t(locale === "zh-CN" ? "switchToEnglish" : "switchToChinese")}
        userName={auth.profile?.displayName || auth.profile?.username || "Atlas"}
        profileLabel={t("sidebarProfile")}
        logoutLabel={t("logout")}
        sidebarTop={<WorkspaceSwitcher workspaceId={workspace.id} workspaceLabel={workspace.name || workspace.appKey} />}
        onNavigate={navigate}
        onToggleLocale={() => setLocale(locale === "zh-CN" ? "en-US" : "zh-CN")}
        onOpenProfile={() => navigate(orgWorkspaceSettingsPath(organization, workspace.id, "profile"))}
        onLogout={() => {
          void auth.logout().then(() => navigate(signPath(), { replace: true }));
        }}
      >
        <StudioContextProvider api={studioApi}>
          <Outlet />
        </StudioContextProvider>
      </CozeShell>
    </>
  );
}

function WorkspaceLibraryRoute() {
  const workspace = useWorkspaceContext();
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  const workspaceLibraryApi = useMemo<LibraryKnowledgeApi>(() => ({
    ...libraryApi,
    createKnowledgeBase: request => createKnowledgeBase({ ...request, workspaceId: Number(workspace.id) }),
    updateKnowledgeBase: (knowledgeBaseId, request) => updateKnowledgeBase(knowledgeBaseId, { ...request, workspaceId: Number(workspace.id) })
  }), [workspace.id]);
  return <LibraryPage api={workspaceLibraryApi} locale={locale} appKey={workspace.appKey} spaceId={workspace.id} onNavigate={navigate} />;
}

function WorkspaceKnowledgeDetailRoute() {
  const { id = "" } = useParams();
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  const workspace = useWorkspaceContext();
  const workspaceLibraryApi = useMemo<LibraryKnowledgeApi>(() => ({
    ...libraryApi,
    createKnowledgeBase: request => createKnowledgeBase({ ...request, workspaceId: Number(workspace.id) }),
    updateKnowledgeBase: (knowledgeBaseId, request) => updateKnowledgeBase(knowledgeBaseId, { ...request, workspaceId: Number(workspace.id) })
  }), [workspace.id]);
  const tabParam = searchParams.get("tab");
  const allowedTabs = ["overview", "documents", "slices", "retrieval", "bindings", "jobs", "permissions", "versions"] as const;
  type AllowedTab = (typeof allowedTabs)[number];
  const initialTab: AllowedTab = (tabParam && (allowedTabs as readonly string[]).includes(tabParam) ? tabParam : "overview") as AllowedTab;
  return (
    <KnowledgeDetailPage
      api={workspaceLibraryApi}
      locale={locale}
      appKey={workspace.appKey}
      spaceId={workspace.id}
      knowledgeBaseId={Number(id)}
      initialTab={initialTab}
      onTabChange={tab => {
        const next = new URLSearchParams(searchParams);
        next.set("tab", tab);
        setSearchParams(next, { replace: true });
      }}
      onNavigate={navigate}
    />
  );
}

/**
 * v5 §32 / 计划 G8：独立 KB 创建路由 `knowledge-bases/new?kind=text|table|image`。
 * 通过 query 预选 KB 类型，渲染 `KnowledgeBaseCreateWizard` 完整向导，提交后跳转到详情页。
 */
function WorkspaceKnowledgeCreateRoute() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  const workspace = useWorkspaceContext();
  const kindParam = searchParams.get("kind");
  const allowedKinds = ["text", "table", "image"] as const;
  type AllowedKind = (typeof allowedKinds)[number];
  const initialKind: AllowedKind = (kindParam && (allowedKinds as readonly string[]).includes(kindParam) ? kindParam : "text") as AllowedKind;
  const workspaceLibraryApi = useMemo<LibraryKnowledgeApi>(() => ({
    ...libraryApi,
    createKnowledgeBase: request => createKnowledgeBase({ ...request, workspaceId: Number(workspace.id) }),
    updateKnowledgeBase: (knowledgeBaseId, request) => updateKnowledgeBase(knowledgeBaseId, { ...request, workspaceId: Number(workspace.id) })
  }), [workspace.id]);
  return (
    <KnowledgeBaseCreateWizard
      api={workspaceLibraryApi}
      locale={locale}
      visible={true}
      initialKind={initialKind}
      onCreated={kbId =>
        navigate(`/apps/${encodeURIComponent(workspace.appKey)}/studio/knowledge-bases/${kbId}?tab=overview`)
      }
      onCancel={() => navigate(`/apps/${encodeURIComponent(workspace.appKey)}/studio/library`)}
    />
  );
}

function WorkspaceKnowledgeJobsCenterRoute() {
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  const workspace = useWorkspaceContext();
  return (
    <KnowledgeJobsCenterPage
      api={libraryApi}
      locale={locale}
      appKey={workspace.appKey}
      spaceId={workspace.id}
      onNavigate={navigate}
    />
  );
}

function WorkspaceKnowledgeProviderCenterRoute() {
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  const workspace = useWorkspaceContext();
  return (
    <KnowledgeProviderConfigPage
      api={libraryApi}
      locale={locale}
      appKey={workspace.appKey}
      onNavigate={navigate}
    />
  );
}

function WorkspaceKnowledgeUploadRoute() {
  const { id = "" } = useParams();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  const workspace = useWorkspaceContext();
  const workspaceLibraryApi = useMemo<LibraryKnowledgeApi>(() => ({
    ...libraryApi,
    createKnowledgeBase: request => createKnowledgeBase({ ...request, workspaceId: Number(workspace.id) }),
    updateKnowledgeBase: (knowledgeBaseId, request) => updateKnowledgeBase(knowledgeBaseId, { ...request, workspaceId: Number(workspace.id) })
  }), [workspace.id]);
  return <KnowledgeUploadPage api={workspaceLibraryApi} locale={locale} appKey={workspace.appKey} spaceId={workspace.id} knowledgeBaseId={Number(id)} initialType={searchParams.get("type")} onNavigate={navigate} />;
}

function WorkspaceDevelopRoute() {
  return <WorkspaceStudioRoute defaultFocus="overview" />;
}

function WorkspaceManageRoute() {
  const { tab = "overview" } = useParams();
  const orgId = useResolvedOrgId();
  const workspace = useWorkspaceContext();

  switch (tab) {
    case "overview":
      return <AdminOverviewRoute />;
    case "users":
      return <AdminUsersRoute />;
    case "roles":
      return <AdminRolesRoute />;
    case "departments":
      return <AdminDepartmentsRoute />;
    case "positions":
      return <AdminPositionsRoute />;
    case "approval":
      return <AdminApprovalRoute />;
    case "reports":
      return <AdminReportsRoute />;
    case "dashboards":
      return <AdminDashboardsRoute />;
    case "visualization":
      return <AdminVisualizationRoute />;
    default:
      return <Navigate to={orgWorkspaceManagePath(orgId, workspace.id, "overview")} replace />;
  }
}

function WorkspaceSettingsRoute() {
  const { tab = "members" } = useParams();
  const orgId = useResolvedOrgId();
  const workspace = useWorkspaceContext();
  const { locale } = useAppI18n();
  const { adminApi } = useAppApis(workspace.appKey);

  if (tab === "system") {
    return <SettingsAdminPage api={adminApi} locale={locale} />;
  }

  if (tab === "profile") {
    return <ProfileAdminPage api={adminApi} locale={locale} />;
  }

  if (tab !== "members" && tab !== "permissions") {
    return <Navigate to={orgWorkspaceSettingsPath(orgId, workspace.id, "members")} replace />;
  }

  return <WorkspaceAccessSettingsRoute activeTab={tab} />;
}

function WorkspaceAccessSettingsRoute({
  activeTab
}: {
  activeTab: "members" | "permissions";
}) {
  const orgId = useResolvedOrgId();
  const workspace = useWorkspaceContext();
  const navigate = useNavigate();
  const { t } = useAppI18n();

  type MemberSearchResult = {
    id: string;
    username: string;
    displayName: string;
    isActive: boolean;
    disabledReason?: string;
    currentRoleCode?: string;
  };

  const [membersLoading, setMembersLoading] = useState(true);
  const [memberSearchLoading, setMemberSearchLoading] = useState(false);
  const [memberSearchPageIndex, setMemberSearchPageIndex] = useState(1);
  const [memberSearchPageSize] = useState(20);
  const [memberSearchTotal, setMemberSearchTotal] = useState(0);
  const [permissionsLoading, setPermissionsLoading] = useState(false);
  const [resourcesLoading, setResourcesLoading] = useState(true);
  const [members, setMembers] = useState<WorkspaceMemberDto[]>([]);
  const [memberSearchResults, setMemberSearchResults] = useState<MemberSearchResult[]>([]);
  const [resources, setResources] = useState<WorkspaceResourceCardDto[]>([]);
  const [selectedResourceKey, setSelectedResourceKey] = useState("");
  const [selectedPermissions, setSelectedPermissions] = useState<WorkspaceRolePermissionDto[]>([]);

  const refreshMembers = async () => {
    setMembersLoading(true);
    try {
      setMembers(await getWorkspaceMembers(orgId, workspace.id));
    } finally {
      setMembersLoading(false);
    }
  };

  const refreshResources = async () => {
    setResourcesLoading(true);
    try {
      const result = await getWorkspaceResources(orgId, workspace.id, undefined, { pageIndex: 1, pageSize: 50 });
      setResources(result.items);
      if (!selectedResourceKey && result.items[0]) {
        setSelectedResourceKey(`${result.items[0].resourceType}:${result.items[0].resourceId}`);
      }
    } finally {
      setResourcesLoading(false);
    }
  };

  const searchMembers = async (keyword: string, pageIndex = 1) => {
    const normalized = keyword.trim();
    if (!normalized) {
      setMemberSearchPageIndex(1);
      setMemberSearchTotal(0);
      setMemberSearchResults([]);
      return;
    }

    setMemberSearchLoading(true);
    try {
      const result = await getUsersPaged({ pageIndex, pageSize: memberSearchPageSize, keyword: normalized });
      const memberMap = new Map(members.map(item => [item.userId, item]));
      const nextResults = result.items.map(item => ({
        id: item.id,
        username: item.username,
        displayName: item.displayName,
        isActive: item.isActive,
        currentRoleCode: memberMap.get(item.id)?.roleCode,
        disabledReason: memberMap.has(item.id)
          ? t("workspaceSettingsMemberAlreadyExists")
          : !item.isActive
            ? t("workspaceSettingsMemberDisabledHint")
            : undefined
      }));
      setMemberSearchPageIndex(pageIndex);
      setMemberSearchTotal(result.total);
      setMemberSearchResults(nextResults);
    } finally {
      setMemberSearchLoading(false);
    }
  };

  const refreshPermissions = async () => {
    if (!selectedResourceKey) {
      setSelectedPermissions([]);
      return;
    }

    const [resourceType, resourceId] = selectedResourceKey.split(":");
    if (!resourceType || !resourceId) {
      setSelectedPermissions([]);
      return;
    }

    setPermissionsLoading(true);
    try {
      setSelectedPermissions(await getWorkspaceResourcePermissions(orgId, workspace.id, resourceType, resourceId));
    } finally {
      setPermissionsLoading(false);
    }
  };

  useEffect(() => {
    void refreshMembers();
    void refreshResources();
  }, [orgId, workspace.id]);

  useEffect(() => {
    void refreshPermissions();
  }, [orgId, selectedResourceKey, workspace.id]);

  return (
    <WorkspaceSettingsPage
      activeTab={activeTab}
      workspaceName={workspace.name}
      membersLoading={membersLoading}
      memberSearchLoading={memberSearchLoading}
      memberSearchPageIndex={memberSearchPageIndex}
      memberSearchPageSize={memberSearchPageSize}
      memberSearchTotal={memberSearchTotal}
      permissionsLoading={permissionsLoading}
      resourcesLoading={resourcesLoading}
      members={members}
      memberSearchResults={memberSearchResults}
      resources={resources}
      selectedResourceKey={selectedResourceKey}
      selectedPermissions={selectedPermissions}
      onSelectResource={setSelectedResourceKey}
      onTabChange={nextTab => navigate(orgWorkspaceSettingsPath(orgId, workspace.id, nextTab))}
      onSearchMembers={searchMembers}
      onRefreshMembers={refreshMembers}
      onRefreshPermissions={refreshPermissions}
      onAddMember={request => addWorkspaceMember(orgId, workspace.id, request).then(refreshMembers)}
      onUpdateMemberRole={(userId, roleCode) => updateWorkspaceMemberRole(orgId, workspace.id, userId, { roleCode }).then(refreshMembers)}
      onRemoveMember={userId => removeWorkspaceMember(orgId, workspace.id, userId).then(refreshMembers)}
      onSavePermissions={items => {
        const [resourceType, resourceId] = selectedResourceKey.split(":");
        return updateWorkspaceResourcePermissions(orgId, workspace.id, resourceType, resourceId, { items }).then(refreshPermissions);
      }}
    />
  );
}

function WorkspaceAppDetailRoute() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  const workspace = useWorkspaceContext();
  const orgId = useResolvedOrgId();
  const { studioApi } = useAppApis(workspace.appKey);
  return (
    <AppDetailPage
      api={studioApi}
      locale={locale}
      appId={id}
      onOpenWorkflow={workflowId => navigate(orgWorkspaceAppWorkflowPath(orgId, workspace.id, id, workflowId))}
      onOpenPublish={() => navigate(orgWorkspaceAppPublishPath(orgId, workspace.id, id))}
    />
  );
}

function WorkspaceAppPublishRoute() {
  const { id = "" } = useParams();
  const { locale } = useAppI18n();
  const workspace = useWorkspaceContext();
  const { studioApi } = useAppApis(workspace.appKey);
  return <AppPublishPage api={studioApi} locale={locale} appId={id} />;
}

function WorkspaceAgentDetailRoute() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const { locale } = useAppI18n();
  const workspace = useWorkspaceContext();
  const orgId = useResolvedOrgId();
  const { studioApi } = useAppApis(workspace.appKey);
  return (
    <BotIdePage
      api={studioApi}
      locale={locale}
      botId={id}
      onOpenPublish={() => navigate(orgWorkspaceAgentPublishPath(orgId, workspace.id, id))}
    />
  );
}

function WorkspaceAgentPublishRoute() {
  const { id = "" } = useParams();
  const { locale } = useAppI18n();
  const workspace = useWorkspaceContext();
  const { studioApi } = useAppApis(workspace.appKey);
  return <AssistantPublishPage api={studioApi} locale={locale} assistantId={id} />;
}

function WorkspaceWorkflowRedirectRoute() {
  return <WorkspaceWorkflowWorkbenchRoute mode="workflow" />;
}

function WorkspaceChatflowRedirectRoute() {
  return <WorkspaceWorkflowWorkbenchRoute mode="chatflow" />;
}

function WorkspaceAppWorkflowRedirectRoute() {
  return <WorkspaceWorkflowWorkbenchRoute mode="workflow" />;
}

function WorkspaceAppChatflowRedirectRoute() {
  return <WorkspaceWorkflowWorkbenchRoute mode="chatflow" />;
}

function StandaloneWorkflowRoute() {
  const { workflowId = "" } = useParams();
  const location = useLocation();
  const bootstrap = useBootstrap();
  const resolvedAppKey = (bootstrap.appKey || getConfiguredAppKey()).trim();

  if (!resolvedAppKey) {
    return <EntryGatewayPage />;
  }

  if (location.pathname.startsWith("/workflow")) {
    const nextSearch = new URLSearchParams(location.search);
    if (workflowId && !nextSearch.has("workflow_id")) {
      nextSearch.set("workflow_id", workflowId);
    }
    const queryText = nextSearch.toString();
    return <Navigate to={`/apps/${encodeURIComponent(resolvedAppKey)}/workflows${queryText ? `?${queryText}` : ""}`} replace />;
  }

  return <Navigate to={`/apps/${encodeURIComponent(resolvedAppKey)}${location.pathname}${location.search}`} replace />;
}

function LegacyAppRedirectRoute() {
  const { appKey = "" } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  const [target, setTarget] = useState<string>("");
  const tenantId = getTenantId() ?? "";

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      if (!tenantId || !appKey) {
        setTarget(signPath());
        return;
      }

      try {
        const workspace = await getWorkspaceByAppKey(tenantId, appKey);
        if (!cancelled) {
          if (!workspace) {
            setTarget(orgWorkspacesPath(tenantId));
            return;
          }

          const currentPath = `${location.pathname}${location.search}`;
          const normalizedAppPrefix = `/apps/${encodeURIComponent(appKey)}`;
          const relativePath = currentPath.startsWith(normalizedAppPrefix)
            ? currentPath.slice(normalizedAppPrefix.length)
            : "";

          setTarget(resolveLegacyAppRedirectTarget({
            orgId: tenantId,
            workspaceId: workspace.id,
            relativePath,
            searchText: location.search
          }));
        }
      } catch {
        if (!cancelled) {
          setTarget(signPath());
        }
      }
    };

    void load();
    return () => {
      cancelled = true;
    };
  }, [appKey, location.pathname, location.search, tenantId]);

  useEffect(() => {
    if (target) {
      navigate(target, { replace: true });
    }
  }, [navigate, target]);

  return <LoadingPage />;
}

interface AppErrorBoundaryState {
  hasError: boolean;
}

class AppErrorBoundary extends Component<{ children: ReactNode }, AppErrorBoundaryState> {
  state: AppErrorBoundaryState = { hasError: false };

  static getDerivedStateFromError(): AppErrorBoundaryState {
    return { hasError: true };
  }

  componentDidCatch(_error: Error, _errorInfo: ErrorInfo): void {
    // Keep the fallback page minimal and avoid recursive state updates.
  }

  render() {
    if (this.state.hasError) {
      return <FatalErrorPage />;
    }

    return this.props.children;
  }
}

function FatalErrorPage() {
  const { t } = useAppI18n();
  const handleReload = () => {
    if (typeof window !== "undefined") {
      window.location.reload();
    }
  };

  return (
    <PageShell centered maxWidth={520}>
      <Card bodyStyle={{ padding: 32 }}>
        <Title heading={3}>{t("fatalErrorTitle")}</Title>
        <Text type="tertiary" style={{ display: "block", marginTop: 8, marginBottom: 16 }}>
          {t("fatalErrorDescription")}
        </Text>
        <div style={{ display: "flex", justifyContent: "flex-end" }}>
          <Button type="primary" theme="solid" onClick={handleReload}>
            {t("fatalErrorReload")}
          </Button>
        </div>
      </Card>
    </PageShell>
  );
}

export const appRoutes = [
  {
    path: "/",
    element: <HomePage />,
    handle: ROOT_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />
  },
  {
    path: "/platform-not-ready",
    element: <PlatformNotReadyPage />,
    handle: STATUS_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />
  },
  {
    path: "/app-setup",
    element: <AppSetupPage />,
    handle: STATUS_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />
  },
  {
    path: "/setup-console",
    element: <SetupConsolePage />,
    handle: SETUP_CONSOLE_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />
  },
  {
    path: "/setup-console/:tab",
    element: <SetupConsolePage />,
    handle: SETUP_CONSOLE_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />
  },
  {
    path: "/sign",
    element: <LoginPage />,
    handle: SIGN_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />
  },
  {
    path: "/select-workspace",
    element: <SelectWorkspacePage />,
    handle: WORKSPACE_LIST_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />
  },
  {
    path: "/workspace/:workspaceId",
    element: <WorkspaceShellLayout />,
    handle: WORKSPACE_SHELL_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />,
    children: [
      { index: true, element: <Navigate to="home" replace /> },
      { path: "home", element: <LegacyWorkspaceHomeRedirect /> },
      { path: "projects", element: <WorkspaceProjectsPage /> },
      { path: "projects/folder/:folderId", element: <WorkspaceProjectsPage /> },
      { path: "resources", element: <WorkspaceResourcesPage /> },
      { path: "resources/:type", element: <WorkspaceResourcesPage /> },
      { path: "tasks", element: <WorkspaceTasksPage /> },
      { path: "tasks/:taskId", element: <WorkspaceTasksPage /> },
      { path: "evaluations", element: <WorkspaceEvaluationsPage /> },
      { path: "evaluations/:evaluationId", element: <WorkspaceEvaluationsPage /> },
      { path: "settings", element: <Navigate to="publish" replace /> },
      { path: "settings/publish", element: <WorkspaceSettingsPublishPage /> },
      { path: "settings/publish/:tab", element: <WorkspaceSettingsPublishPage /> },
      { path: "settings/models", element: <WorkspaceSettingsModelsPage /> }
    ]
  },
  {
    path: "/me",
    element: <PlatformShellLayout />,
    handle: WORKSPACE_SHELL_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />,
    children: [
      { index: true, element: <Navigate to="profile" replace /> },
      { path: "profile", element: <MeProfilePage /> },
      { path: "settings", element: <Navigate to="account" replace /> },
      { path: "settings/:tab", element: <MeSettingsPage /> },
      { path: "notifications", element: <MeNotificationsPage /> }
    ]
  },
  {
    path: "/market",
    element: <PlatformShellLayout />,
    handle: WORKSPACE_SHELL_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />,
    children: [
      { index: true, element: <Navigate to="templates" replace /> },
      { path: "templates", element: <MarketTemplatesPage /> },
      { path: "plugins", element: <MarketPluginsPage /> }
    ]
  },
  {
    path: "/community",
    element: <PlatformShellLayout />,
    handle: WORKSPACE_SHELL_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />,
    children: [
      { index: true, element: <Navigate to="works" replace /> },
      { path: "works", element: <CommunityWorksPage /> }
    ]
  },
  {
    path: "/open",
    element: <PlatformShellLayout />,
    handle: WORKSPACE_SHELL_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />,
    children: [
      { index: true, element: <Navigate to="api" replace /> },
      { path: "api", element: <OpenApiPage /> }
    ]
  },
  {
    path: "/docs",
    element: <PlatformShellLayout />,
    handle: WORKSPACE_SHELL_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />,
    children: [
      { index: true, element: <DocsPage /> },
      { path: ":slug", element: <DocsPage /> }
    ]
  },
  {
    path: "/platform",
    element: <PlatformShellLayout />,
    handle: WORKSPACE_SHELL_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />,
    children: [
      { index: true, element: <Navigate to="general" replace /> },
      { path: "general", element: <PlatformGeneralPage /> }
    ]
  },
  {
    path: "/agent",
    element: <EditorShellLayout />,
    handle: WORKSPACE_DEVELOP_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />,
    children: [
      { path: ":agentId/editor", element: <AgentEditorRoute /> },
      { path: ":agentId/publish", element: <AgentPublishRoute /> }
    ]
  },
  {
    path: "/app",
    element: <EditorShellLayout />,
    handle: WORKSPACE_DEVELOP_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />,
    children: [
      { path: ":projectId/editor", element: <AppEditorRoute /> },
      { path: ":projectId/publish", element: <AppPublishRoute /> }
    ]
  },
  {
    path: "/workflow/:workflowId/editor",
    element: <EditorShellLayout />,
    handle: WORKSPACE_WORKFLOW_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />,
    children: [{ index: true, element: <WorkflowEditorRoute /> }]
  },
  {
    path: "/chatflow/:chatflowId/editor",
    element: <EditorShellLayout />,
    handle: WORKSPACE_CHATFLOW_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />,
    children: [{ index: true, element: <ChatflowEditorRoute /> }]
  },
  {
    path: "/workflows",
    element: <StandaloneWorkflowRoute />,
    handle: STANDALONE_WORKFLOW_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />
  },
  {
    path: "/workflow",
    element: <StandaloneWorkflowRoute />,
    handle: STANDALONE_WORKFLOW_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />
  },
  {
    path: "/workflow/:workflowId",
    element: <StandaloneWorkflowRoute />,
    handle: STANDALONE_WORKFLOW_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />
  },
  {
    path: "/org/:orgId/workspaces",
    element: <WorkspaceListRoute />,
    handle: WORKSPACE_LIST_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />
  },
  {
    path: "/org/:orgId/workspaces/:workspaceId",
    element: <WorkspaceShellRoute />,
    handle: WORKSPACE_SHELL_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />,
    children: [
      {
        index: true,
        element: <LegacyOrgWorkspaceHomeRedirect />,
        handle: WORKSPACE_DASHBOARD_ROUTE_HANDLE
      },
      {
        path: "home",
        element: <WorkspaceHomePage />,
        handle: WORKSPACE_DASHBOARD_ROUTE_HANDLE
      },
      {
        path: "dashboard",
        element: <LegacyOrgWorkspaceHomeRedirect />,
        handle: WORKSPACE_DASHBOARD_ROUTE_HANDLE
      },
      {
        path: "develop",
        element: <WorkspaceDevelopRoute />,
        handle: WORKSPACE_DEVELOP_ROUTE_HANDLE
      },
      {
        path: "develop/chat",
        element: <AgentChatRoute />,
        handle: WORKSPACE_DEVELOP_ROUTE_HANDLE
      },
      {
        path: "develop/model-configs",
        element: <ModelConfigsRoute />,
        handle: WORKSPACE_DEVELOP_ROUTE_HANDLE
      },
      {
        path: "develop/assistant-tools",
        element: <StudioAssistantToolsRoute />,
        handle: WORKSPACE_DEVELOP_ROUTE_HANDLE
      },
      {
        path: "develop/publish-center",
        element: <StudioPublishCenterRoute />,
        handle: WORKSPACE_DEVELOP_ROUTE_HANDLE
      },
      {
        path: "library",
        element: (
          <ProtectedPage permission={APP_PERMISSIONS.KNOWLEDGE_BASE_VIEW}>
            <WorkspaceLibraryRoute />
          </ProtectedPage>
        ),
        handle: WORKSPACE_LIBRARY_ROUTE_HANDLE
      },
      {
        path: "library/jobs",
        element: (
          <ProtectedPage permission={APP_PERMISSIONS.KNOWLEDGE_BASE_VIEW}>
            <WorkspaceKnowledgeJobsCenterRoute />
          </ProtectedPage>
        ),
        handle: WORKSPACE_LIBRARY_ROUTE_HANDLE
      },
      {
        path: "library/providers",
        element: (
          <ProtectedPage permission={APP_PERMISSIONS.KNOWLEDGE_BASE_VIEW}>
            <WorkspaceKnowledgeProviderCenterRoute />
          </ProtectedPage>
        ),
        handle: WORKSPACE_LIBRARY_ROUTE_HANDLE
      },
      {
        path: "library/data",
        element: <StudioDataRoute />,
        handle: WORKSPACE_LIBRARY_ROUTE_HANDLE
      },
      {
        path: "library/variables",
        element: <StudioVariablesRoute />,
        handle: WORKSPACE_LIBRARY_ROUTE_HANDLE
      },
      {
        path: "apps/:id",
        element: <WorkspaceAppDetailRoute />,
        handle: WORKSPACE_DEVELOP_ROUTE_HANDLE
      },
      {
        path: "apps/:id/publish",
        element: <WorkspaceAppPublishRoute />,
        handle: WORKSPACE_DEVELOP_ROUTE_HANDLE
      },
      {
        path: "apps/:id/workflows/:workflowId",
        element: <WorkspaceAppWorkflowRedirectRoute />,
        handle: WORKSPACE_WORKFLOW_ROUTE_HANDLE
      },
      {
        path: "apps/:id/chatflows/:workflowId",
        element: <WorkspaceAppChatflowRedirectRoute />,
        handle: WORKSPACE_CHATFLOW_ROUTE_HANDLE
      },
      {
        path: "agents/:id",
        element: <WorkspaceAgentDetailRoute />,
        handle: WORKSPACE_DEVELOP_ROUTE_HANDLE
      },
      {
        path: "agents/:id/publish",
        element: <WorkspaceAgentPublishRoute />,
        handle: WORKSPACE_DEVELOP_ROUTE_HANDLE
      },
      {
        path: "workflows",
        element: <WorkspaceWorkflowWorkbenchRoute mode="workflow" />,
        handle: WORKSPACE_WORKFLOW_ROUTE_HANDLE
      },
      {
        path: "workflows/:id",
        element: <WorkspaceWorkflowRedirectRoute />,
        handle: WORKSPACE_WORKFLOW_ROUTE_HANDLE
      },
      {
        path: "chatflows",
        element: <WorkspaceWorkflowWorkbenchRoute mode="chatflow" />,
        handle: WORKSPACE_CHATFLOW_ROUTE_HANDLE
      },
      {
        path: "chatflows/:id",
        element: <WorkspaceChatflowRedirectRoute />,
        handle: WORKSPACE_CHATFLOW_ROUTE_HANDLE
      },
      {
        path: "knowledge-bases/:id",
        element: (
          <ProtectedPage permission={APP_PERMISSIONS.KNOWLEDGE_BASE_VIEW}>
            <WorkspaceKnowledgeDetailRoute />
          </ProtectedPage>
        ),
        handle: WORKSPACE_LIBRARY_ROUTE_HANDLE
      },
      {
        // v5 §32 / 计划 G8：独立创建路由，支持 ?kind=text|table|image 预选 KB 类型
        path: "knowledge-bases/new",
        element: (
          <ProtectedPage permission={APP_PERMISSIONS.KNOWLEDGE_BASE_UPDATE}>
            <WorkspaceKnowledgeCreateRoute />
          </ProtectedPage>
        ),
        handle: WORKSPACE_LIBRARY_ROUTE_HANDLE
      },
      {
        path: "knowledge-bases/:id/upload",
        element: (
          <ProtectedPage permission={APP_PERMISSIONS.KNOWLEDGE_BASE_UPDATE}>
            <WorkspaceKnowledgeUploadRoute />
          </ProtectedPage>
        ),
        handle: WORKSPACE_LIBRARY_ROUTE_HANDLE
      },
      {
        path: "databases/:id",
        element: <StudioDatabaseDetailRoute />,
        handle: WORKSPACE_LIBRARY_ROUTE_HANDLE
      },
      {
        path: "plugins/:id",
        element: <StudioPluginDetailRoute />,
        handle: WORKSPACE_DEVELOP_ROUTE_HANDLE
      },
      {
        path: "plugins/:id/tools/:toolId",
        element: <WorkspacePluginToolRoute />,
        handle: WORKSPACE_DEVELOP_ROUTE_HANDLE
      },
      {
        path: "plugin/:id/tool/:toolId",
        element: <WorkspacePluginToolRoute />,
        handle: WORKSPACE_DEVELOP_ROUTE_HANDLE
      },
      {
        path: "manage",
        element: <Navigate to="overview" replace />,
        handle: WORKSPACE_MANAGE_ROUTE_HANDLE
      },
      {
        path: "manage/:tab",
        element: <WorkspaceManageRoute />,
        handle: WORKSPACE_MANAGE_ROUTE_HANDLE
      },
      {
        path: "settings",
        element: <Navigate to="members" replace />,
        handle: WORKSPACE_SETTINGS_ROUTE_HANDLE
      },
      {
        path: "settings/:tab",
        element: <WorkspaceSettingsRoute />,
        handle: WORKSPACE_SETTINGS_ROUTE_HANDLE
      },
      {
        path: "settings/connectors",
        element: <ConnectorsListRoute />
      },
      {
        path: "settings/connectors/:providerId",
        element: <ConnectorDetailRoute />
      }
    ]
  },
  {
    path: "/explore/plugin/:productId",
    element: <ExplorePluginDetailRoute />,
    handle: EXPLORE_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />
  },
  {
    path: "/explore/plugin",
    element: <ExplorePluginsRoute />,
    handle: EXPLORE_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />
  },
  {
    path: "/explore/template/:templateId",
    element: <ExploreTemplateDetailRoute />,
    handle: EXPLORE_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />
  },
  {
    path: "/explore/template",
    element: <ExploreTemplatesRoute />,
    handle: EXPLORE_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />
  },
  {
    path: "/search/:word",
    element: <ExploreSearchRoute />,
    handle: EXPLORE_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />
  },
  {
    path: "/apps/:appKey/*",
    element: <LegacyAppRedirectRoute />,
    handle: STATUS_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />
  },
  {
    path: "/forbidden",
    element: <ForbiddenPage />,
    handle: STATUS_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />
  },
  {
    path: "*",
    element: <Navigate to="/" replace />,
    handle: ROOT_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />
  }
];

const ConnectorsLazy = React.lazy(() => import("./pages/connectors/connectors-page"));
const ConnectorDetailLazy = React.lazy(() => import("./pages/connectors/connector-detail-page"));

function ConnectorsListRoute() {
  return (
    <Suspense fallback={<LoadingPage />}>
      <ConnectorsLazy />
    </Suspense>
  );
}

function ConnectorDetailRoute() {
  return (
    <Suspense fallback={<LoadingPage />}>
      <ConnectorDetailLazy />
    </Suspense>
  );
}

const appRouter = createBrowserRouter(appRoutes, {
  future: {
    v7_startTransition: true,
    v7_relativeSplatPath: true,
  },
});

export function AppRouter() {
  return (
    <Suspense fallback={<LoadingPage />}>
      <RouterProvider router={appRouter} fallbackElement={<LoadingPage />} />
    </Suspense>
  );
}

export function AppRoot() {
  return (
    <AppI18nProvider>
      <AppErrorBoundary>
        <BootstrapProvider>
          <AuthProvider>
            <AppStartupKernel loadingFallback={<LoadingPage />}>
              <AppRouter />
            </AppStartupKernel>
          </AuthProvider>
        </BootstrapProvider>
      </AppErrorBoundary>
    </AppI18nProvider>
  );
}

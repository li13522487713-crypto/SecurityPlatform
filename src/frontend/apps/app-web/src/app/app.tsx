/* eslint-disable @typescript-eslint/no-unused-vars */
import * as React from "react";
import { Component, Suspense, useCallback, useEffect, useMemo, useState, type ErrorInfo, type ReactElement, type ReactNode } from "react";
import { createBrowserRouter, Navigate, Outlet, RouterProvider, useLocation, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { Button, Card, Typography } from "@douyinfe/semi-ui";
import { getTenantId } from "@atlas/shared-react-core/utils";
import { PageShell, ResultCard } from "./_shared";

const { Title, Text } = Typography;
import type { LibraryKnowledgeApi, AiLibraryItem } from "@atlas/library-module-react";
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
  workspaceResourcesPath,
  selectWorkspacePath,
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
  orgWorkspaceWorkflowsPath,
  orgWorkspaceChatflowsPath,
  orgWorkspaceWorkflowPath,
  orgWorkspaceChatflowPath,
  orgWorkspaceKnowledgeBaseDetailPath,
  orgWorkspaceKnowledgeBaseUploadPath,
  orgWorkspaceDatabaseDetailPath,
  orgWorkspacePluginDetailPath,
  orgWorkspaceSettingsPath,
  lowcodeAppStudioPath,
  signPath
} from "@atlas/app-shell-shared";
import { lazyNamed } from "./lazy-named";
import type { AppLocale } from "./messages";
import { AuthProvider, useAuth } from "./auth-context";
import { BootstrapProvider, useBootstrap } from "./bootstrap-context";
import { AppI18nProvider, useAppI18n } from "./i18n";
import { OrganizationProvider, useOptionalOrganizationContext } from "./organization-context";
import { PermissionProvider } from "./permission-context";
import {
  type AppRouteHandle,
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
import { CozeAgentPublishManagePage } from "./pages/coze-agent-publish-manage-page";
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
import { PlatformShellLayout, SpaceShellLayout, readLastWorkspaceId, rememberLastWorkspaceId } from "./layouts/workspace-shell";
import { EditorShellLayout } from "./layouts/editor-shell";
import { WorkspaceSwitcher } from "./components/workspace-switcher";
import {
  AgentEditorRoute,
  AgentPublishRoute,
  AppEditorRoute,
  AppPublishRoute,
  CanonicalLowcodeStudioRoute,
  ChatflowEditorRoute,
  WorkflowEditorRoute
} from "./pages/editor-routes";
import { WorkspaceHomePage } from "./pages/workspace-home-page";
import { WorkspaceProjectsPage } from "./pages/workspace-projects-page";
import { WorkspaceResourcesRedirect } from "./pages/workspace-resources-redirect";
import { WorkspaceLibraryPage } from "./pages/workspace-library-page";
import { WorkspaceTasksPage } from "./pages/workspace-tasks-page";
import { WorkspaceEvaluationsPage } from "./pages/workspace-evaluations-page";
import { CozeWorkspaceConsolePage } from "./pages/coze-workspace-console-page";
import { WorkspaceSettingsPublishPage } from "./pages/workspace-settings-publish-page";
import { WorkspaceSettingsModelsPage } from "./pages/workspace-settings-models-page";
import { MarketTemplatesPage } from "./pages/market-templates-page";
import { MarketPluginsPage } from "./pages/market-plugins-page";
import { CommunityWorksPage } from "./pages/community-works-page";
import { OpenApiPage } from "./pages/open-api-page";
import { DocsPage } from "./pages/docs-page";
import { MeProfilePage } from "./pages/me-profile-page";
import { MeSettingsPage } from "./pages/me-settings-page";
import { MeNotificationsPage } from "./pages/me-notifications-page";
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
  deleteAiDatabaseRecordWithEnvironment,
  getAiDatabasesPaged,
  deleteAiDatabase,
  getAiDatabaseById,
  getAiDatabaseChannelConfigs,
  getAiDatabaseImportProgress,
  getAiDatabaseRecordsPaged,
  submitAiDatabaseImport,
  updateAiDatabaseRecord,
  updateAiDatabaseChannelConfigs,
  updateAiDatabaseMode,
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
  bindAiAssistantDatabase,
  generateByAiAssistant,
  bindAiAssistantWorkflow,
  createAiAssistant,
  getAiAssistantById,
  getAiAssistantsPaged,
  getAiAssistantPublications,
  publishAiAssistant,
  regenerateAiAssistantEmbedToken,
  unbindAiAssistantDatabase,
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
  getWorkspaceByAppKey,
  getWorkspaceMembers,
  getWorkspaceResourcePermissions,
  getWorkspaceResources,
  getWorkspaces,
  removeWorkspaceMember,
  type WorkspaceMemberDto,
  type WorkspaceResourceCardDto,
  type WorkspaceRolePermissionDto,
  updateWorkspaceMemberRole,
  updateWorkspaceResourcePermissions
} from "../services/api-org-workspaces";
import { WorkspaceSettingsPage } from "./pages/workspace-settings-page";
import { setAppInstanceIdToStorage } from "../utils/app-context";

type WorkflowResourceMode = "workflow" | "chatflow";
type WorkflowWorkbenchContentMode = "canvas" | "variables" | "session";

const loadLibraryModule = () => import("@atlas/library-module-react");
const loadAdminModule = () => import("@atlas/module-admin-react");
const loadExploreModule = () => import("@atlas/module-explore-react");
const loadStudioModule = () => import("@atlas/module-studio-react");
const loadCozeWorkspaceLibraryModule = () => import("@coze-studio/workspace-adapter/library");
const loadCozeAgentEditorModule = () => import("@coze-agent-ide/entry-adapter");
const loadCozeAgentPublishModule = () => import("@coze-agent-ide/agent-publish");
const loadCozeWorkflowPlaygroundModule = () => import("@coze-workflow/playground-adapter");


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
const CozeWorkspaceLibraryPage = lazyNamed(loadCozeWorkspaceLibraryModule, "LibraryPage");
const CozeBotEditorPage = lazyNamed(loadCozeAgentEditorModule, "BotEditor");
const CozeAgentPublishPage = lazyNamed(loadCozeAgentPublishModule, "AgentPublishPage");
const CozeWorkflowPage = lazyNamed(loadCozeWorkflowPlaygroundModule, "WorkflowPage");
const CozeAgentLayout = React.lazy(() => import("@coze-agent-ide/layout-adapter"));

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

    return getLibraryPaged(request, { resourceType: normalizedType }).then(result => ({
      ...result,
      items: result.items.filter(item =>
        item.resourceType === "workflow"
        || item.resourceType === "plugin"
        || item.resourceType === "knowledge-base"
        || item.resourceType === "database"
        || item.resourceType === "agent"
        || item.resourceType === "app"
        || item.resourceType === "prompt"
      ) as AiLibraryItem[]
    }));
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

function normalizeWorkspaceIdValue(rawValue?: string | null): string | undefined {
  if (!rawValue) {
    return undefined;
  }

  const normalized = rawValue.trim();
  return /^\d+$/.test(normalized) ? normalized : undefined;
}

function buildWorkspaceWorkbenchPath(
  orgId: string,
  workspaceId: string,
  mode: WorkflowResourceMode,
  workflowId?: string,
  contentMode: WorkflowWorkbenchContentMode = "canvas",
  searchParams?: URLSearchParams
): string {
  const pathname = workflowId
    ? mode === "chatflow"
      ? orgWorkspaceChatflowPath(orgId, workspaceId, workflowId)
      : orgWorkspaceWorkflowPath(orgId, workspaceId, workflowId)
    : mode === "chatflow"
      ? orgWorkspaceChatflowsPath(orgId, workspaceId)
      : orgWorkspaceWorkflowsPath(orgId, workspaceId);

  const nextSearch = new URLSearchParams(searchParams ?? undefined);
  if (contentMode === "canvas") {
    nextSearch.delete("contentMode");
  } else {
    nextSearch.set("contentMode", contentMode);
  }

  if (workflowId) {
    nextSearch.delete("workflow_id");
  }

  const query = nextSearch.toString();
  return query ? `${pathname}?${query}` : pathname;
}

export function createStudioApi(appKey: string, workspaceId?: string): StudioModuleApi {
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
      const result = await createWorkspaceIdeApp({
        ...request,
        workspaceId: request.workspaceId ?? workspaceId
      });
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
      const result = await getAiPluginsPaged({ pageIndex: 1, pageSize: 50 }, undefined, workspaceId);
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
      const result = await getKnowledgeBasesPaged({ pageIndex: 1, pageSize: 50 }, undefined, workspaceId);
      return result.items.map(item => ({
        id: item.id,
        name: item.name,
        type: item.type
      }));
    },
    getKnowledgeBase: getKnowledgeBaseById,
    listDatabases: async () => {
      const result = await getAiDatabasesPaged({ pageIndex: 1, pageSize: 50 }, undefined, workspaceId);
      return result.items.map(item => ({
        id: item.id,
        name: item.name,
        botId: item.botId
      }));
    },
    createDatabase: createAiDatabase,
    updateDatabase: updateAiDatabase,
    getDatabaseDetail: getAiDatabaseById,
    listDatabaseRecords: (id, params) =>
      getAiDatabaseRecordsPaged(id, {
        pageIndex: params?.pageIndex ?? 1,
        pageSize: params?.pageSize ?? 10
      }, params?.environment as never),
    createDatabaseRecord: createAiDatabaseRecord,
    updateDatabaseRecord: updateAiDatabaseRecord,
    deleteDatabaseRecord: (id, recordId, environment) =>
      environment !== undefined
        ? deleteAiDatabaseRecordWithEnvironment(id, recordId, environment as never)
        : deleteAiDatabaseRecord(id, recordId),
    validateDatabaseSchemaDraft: validateAiDatabaseSchema,
    submitDatabaseImport: (id, file, environment) =>
      submitAiDatabaseImport(id, file, environment as never),
    getDatabaseImportProgress: getAiDatabaseImportProgress,
    downloadDatabaseTemplate: downloadAiDatabaseTemplate,
    getDatabaseChannelConfigs: getAiDatabaseChannelConfigs,
    updateDatabaseChannelConfigs: updateAiDatabaseChannelConfigs,
    updateDatabaseMode: updateAiDatabaseMode,
    // D5：批量插入入口（同步 + 异步）。
    bulkCreateDatabaseRecords: createAiDatabaseRecordsBulk,
    submitDatabaseBulkInsertJob: submitAiDatabaseBulkInsertJob,
    listBotVariables: async (currentBotId: string) => {
      const result = await getAiVariablesPaged(
        { pageIndex: 1, pageSize: 100 },
        { scope: 2, scopeId: currentBotId }
      );
      return result.items.map(item => ({
        id: item.id,
        key: item.key,
        scopeId: item.scopeId
      }));
    },
    bindAgentWorkflow: (agentId, workflowId) => bindAiAssistantWorkflow(agentId, workflowId),
    bindAgentDatabase: (agentId, request) => bindAiAssistantDatabase(agentId, request),
    unbindAgentDatabase: (agentId, databaseId) => unbindAiAssistantDatabase(agentId, databaseId),
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
    listModelConfigs: () => getModelConfigsPaged({ pageIndex: 1, pageSize: 50 }, { workspaceId }),
    getModelConfig: getModelConfigById,
    getModelConfigStats: keyword => getModelConfigStats(keyword, workspaceId),
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
  const workspace = useOptionalWorkspaceContext();
  const resolvedWorkspaceId = normalizeWorkspaceIdValue(workspace?.id);
  return useMemo(() => ({
    adminApi: createAdminApi(appKey),
    exploreApi: createExploreApi(appKey),
    studioApi: createStudioApi(appKey, resolvedWorkspaceId)
  }), [appKey, resolvedWorkspaceId, t]);
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
      onNavigateBack={() => navigate(`${orgWorkspaceLibraryPath(orgId, workspace.id)}?tab=database`)}
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
  const bootstrap = useBootstrap();
  const auth = useAuth();
  const { studioApi } = useAppApis(appKey);

  useEffect(() => {
    rememberConfiguredAppKey(appKey);
  }, [appKey]);

  useEffect(() => {
    if (auth.isAuthenticated && !auth.profile && !auth.loading) {
      void auth.ensureProfile();
    }
  }, [auth.ensureProfile, auth.isAuthenticated, auth.loading, auth.profile]);

  if (bootstrap.loading || auth.loading) {
    return <LoadingPage />;
  }

  if (bootstrap.appKey && bootstrap.appKey !== appKey) {
    return <Navigate to={`${replacePathAppKey(location.pathname, bootstrap.appKey)}${location.search}`} replace />;
  }

  if (!bootstrap.appReady) {
    return <Navigate to="/app-setup" replace />;
  }

  if (!auth.isAuthenticated) {
    return <Navigate to={appSignPath(appKey, location.pathname + location.search)} replace />;
  }

  return (
    <>
      <UnauthorizedNavigationBridge />
      <StudioContextProvider api={studioApi}>
        <Outlet />
      </StudioContextProvider>
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
      onCreateAgent={() => navigate(workspaceProjectsPath(workspace.id))}
      onCreateApp={() => navigate(workspaceProjectsPath(workspace.id))}
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
    <WorkflowRuntimeBoundary spaceId={workspace.id}>
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
  const location = useLocation();

  if (bootstrap.loading || auth.loading) {
    return <LoadingPage />;
  }

  if (!bootstrap.appReady) {
    return <Navigate to="/app-setup" replace />;
  }

  if (!auth.isAuthenticated) {
    return <Navigate to={signPath(location.pathname + location.search)} replace />;
  }

  return <Navigate to={selectWorkspacePath()} replace />;
}

function SpaceEntryRoute() {
  const location = useLocation();
  const auth = useAuth();
  const bootstrap = useBootstrap();

  if (bootstrap.loading || auth.loading) {
    return <LoadingPage />;
  }

  if (!auth.isAuthenticated) {
    return <Navigate to={signPath(location.pathname + location.search)} replace />;
  }

  return <CozeWorkspaceConsolePage />;
}

function WorkspaceShellRoute() {
  return <Navigate to={selectWorkspacePath()} replace />;
}

function WorkspaceShellInner() {
  return <Navigate to={selectWorkspacePath()} replace />;
}

function LegacyConsoleRedirectRoute() {
  return <Navigate to={selectWorkspacePath()} replace />;
}

function WorkspaceLibraryRoute() {
  return <WorkspaceLibraryPage />;
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

function LegacyWorkspaceAppRedirectRoute() {
  const { id = "" } = useParams();
  return <Navigate to={lowcodeAppStudioPath(id)} replace />;
}

function LegacyWorkspaceRouteRedirect() {
  const { workspaceId = "", "*": restPath = "" } = useParams<{ workspaceId: string; "*": string }>();
  const location = useLocation();
  const target = restPath
    ? `/space/${encodeURIComponent(workspaceId)}/${restPath}`
    : `/space/${encodeURIComponent(workspaceId)}`;
  return <Navigate to={`${target}${location.search ?? ""}`} replace />;
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

function StandaloneWorkflowRoute() {
  const { workflowId = "" } = useParams();
  const location = useLocation();
  const query = new URLSearchParams(location.search);
  const queryWorkflowId = query.get("workflow_id")?.trim() ?? "";
  const resolvedWorkflowId = workflowId || queryWorkflowId;
  if (resolvedWorkflowId) {
    return <Navigate to={`/workflow/${encodeURIComponent(resolvedWorkflowId)}/editor`} replace />;
  }

  const fallbackWorkspaceId = readLastWorkspaceId();
  if (!fallbackWorkspaceId) {
    return <Navigate to={selectWorkspacePath()} replace />;
  }

  return <Navigate to={workspaceResourcesPath(fallbackWorkspaceId, "workflows")} replace />;
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

function SpaceProjectAliasRoute() {
  const { id = "" } = useParams();
  return <Navigate to={lowcodeAppStudioPath(id)} replace />;
}

function SpaceBotAnalysisAliasRoute() {
  const { space_id = "", bot_id = "" } = useParams<{ space_id: string; bot_id: string }>();
  return <Navigate to={`/space/${encodeURIComponent(space_id)}/bot/${encodeURIComponent(bot_id)}/publish?tab=analysis`} replace />;
}

function SpaceAgentPublishManageAliasRoute() {
  const { space_id = "", bot_id = "" } = useParams<{ space_id: string; bot_id: string }>();
  const location = useLocation();
  const query = location.search ?? "";
  return <Navigate to={`/space/${encodeURIComponent(space_id)}/bot/${encodeURIComponent(bot_id)}/publish${query}`} replace />;
}

function SpaceAgentEditorRoute() {
  return (
    <Suspense fallback={<PageShell loading testId="coze-agent-editor-loading" />}>
      <CozeBotEditorPage />
    </Suspense>
  );
}

function SpaceAgentPublishRoute() {
  const [searchParams] = useSearchParams();
  const activeTab = searchParams.get("tab");
  if (activeTab === "analysis" || activeTab === "logs" || activeTab === "triggers") {
    return <CozeAgentPublishManagePage />;
  }

  return (
    <Suspense fallback={<PageShell loading testId="coze-agent-publish-loading" />}>
      <CozeAgentPublishPage />
    </Suspense>
  );
}

function SpacePluginDetailRoute() {
  const { id = "" } = useParams();
  const orgId = useResolvedOrgId();
  const workspace = useWorkspaceContext();
  const { locale } = useAppI18n();
  const navigate = useNavigate();
  const { studioApi } = useAppApis(workspace.appKey);
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

function SpaceDatabaseDetailRoute() {
  const { id = "" } = useParams();
  const orgId = useResolvedOrgId();
  const workspace = useWorkspaceContext();
  const { locale } = useAppI18n();
  const navigate = useNavigate();
  const { studioApi } = useAppApis(workspace.appKey);
  return (
    <DatabaseDetailPage
      api={studioApi}
      locale={locale}
      databaseId={Number(id)}
      onOpenLibrary={() => navigate(orgWorkspaceLibraryPath(orgId, workspace.id))}
      onNavigateBack={() => navigate(`${orgWorkspaceLibraryPath(orgId, workspace.id)}?tab=database`)}
    />
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
    path: "/space",
    element: <SpaceEntryRoute />,
    handle: WORKSPACE_LIST_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />
  },
  {
    path: "/space/:space_id",
    element: <SpaceShellLayout />,
    handle: WORKSPACE_SHELL_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />,
    children: [
      { index: true, element: <Navigate to="projects" replace /> },
      { path: "home", element: <WorkspaceHomePage />, handle: WORKSPACE_DASHBOARD_ROUTE_HANDLE },
      { path: "library", element: <WorkspaceLibraryRoute />, handle: { ...WORKSPACE_LIBRARY_ROUTE_HANDLE, subMenuKey: "library" } as AppRouteHandle },
      { path: "projects", element: <WorkspaceProjectsPage />, handle: WORKSPACE_DEVELOP_ROUTE_HANDLE },
      { path: "projects/folder/:folderId", element: <WorkspaceProjectsPage />, handle: WORKSPACE_DEVELOP_ROUTE_HANDLE },
      { path: "resources", element: <WorkspaceResourcesRedirect />, handle: WORKSPACE_LIBRARY_ROUTE_HANDLE },
      { path: "resources/:type", element: <WorkspaceResourcesRedirect />, handle: WORKSPACE_LIBRARY_ROUTE_HANDLE },
      { path: "tasks", element: <WorkspaceTasksPage />, handle: WORKSPACE_MANAGE_ROUTE_HANDLE },
      { path: "tasks/:taskId", element: <WorkspaceTasksPage />, handle: WORKSPACE_MANAGE_ROUTE_HANDLE },
      { path: "evaluations", element: <WorkspaceEvaluationsPage />, handle: WORKSPACE_MANAGE_ROUTE_HANDLE },
      { path: "evaluations/:evaluationId", element: <WorkspaceEvaluationsPage />, handle: WORKSPACE_MANAGE_ROUTE_HANDLE },
      { path: "manage", element: <WorkspaceManageRoute />, handle: WORKSPACE_MANAGE_ROUTE_HANDLE },
      { path: "manage/:tab", element: <WorkspaceManageRoute />, handle: WORKSPACE_MANAGE_ROUTE_HANDLE },
      { path: "settings", element: <Navigate to="publish" replace /> },
      { path: "settings/publish", element: <WorkspaceSettingsPublishPage />, handle: WORKSPACE_SETTINGS_ROUTE_HANDLE },
      { path: "settings/publish/:tab", element: <WorkspaceSettingsPublishPage />, handle: WORKSPACE_SETTINGS_ROUTE_HANDLE },
      { path: "settings/models", element: <WorkspaceSettingsModelsPage />, handle: WORKSPACE_SETTINGS_ROUTE_HANDLE },
      { path: "settings/:tab", element: <WorkspaceSettingsRoute />, handle: WORKSPACE_SETTINGS_ROUTE_HANDLE },
      { path: "project-ide/:id", element: <SpaceProjectAliasRoute />, handle: WORKSPACE_DEVELOP_ROUTE_HANDLE },
      { path: "plugin/:id", element: <SpacePluginDetailRoute />, handle: WORKSPACE_LIBRARY_ROUTE_HANDLE },
      { path: "plugin/:id/tool/:toolId", element: <WorkspacePluginToolRoute />, handle: WORKSPACE_LIBRARY_ROUTE_HANDLE },
      { path: "plugins/:id", element: <SpacePluginDetailRoute />, handle: WORKSPACE_LIBRARY_ROUTE_HANDLE },
      { path: "plugins/:id/tool/:toolId", element: <WorkspacePluginToolRoute />, handle: WORKSPACE_LIBRARY_ROUTE_HANDLE },
      { path: "knowledge/:id", element: <WorkspaceKnowledgeDetailRoute />, handle: WORKSPACE_LIBRARY_ROUTE_HANDLE },
      { path: "knowledge/:id/upload", element: <WorkspaceKnowledgeUploadRoute />, handle: WORKSPACE_LIBRARY_ROUTE_HANDLE },
      { path: "knowledge-bases", element: <WorkspaceLibraryRoute />, handle: WORKSPACE_LIBRARY_ROUTE_HANDLE },
      { path: "knowledge-bases/new", element: <WorkspaceKnowledgeCreateRoute />, handle: WORKSPACE_LIBRARY_ROUTE_HANDLE },
      { path: "knowledge-bases/jobs", element: <WorkspaceKnowledgeJobsCenterRoute />, handle: WORKSPACE_LIBRARY_ROUTE_HANDLE },
      { path: "knowledge-bases/provider-configs", element: <WorkspaceKnowledgeProviderCenterRoute />, handle: WORKSPACE_LIBRARY_ROUTE_HANDLE },
      { path: "knowledge-bases/:id", element: <WorkspaceKnowledgeDetailRoute />, handle: WORKSPACE_LIBRARY_ROUTE_HANDLE },
      { path: "knowledge-bases/:id/upload", element: <WorkspaceKnowledgeUploadRoute />, handle: WORKSPACE_LIBRARY_ROUTE_HANDLE },
      { path: "database/:id", element: <SpaceDatabaseDetailRoute />, handle: WORKSPACE_LIBRARY_ROUTE_HANDLE },
      { path: "databases/:id", element: <SpaceDatabaseDetailRoute />, handle: WORKSPACE_LIBRARY_ROUTE_HANDLE },
      { path: "apps/:id", element: <LegacyWorkspaceAppRedirectRoute />, handle: WORKSPACE_DEVELOP_ROUTE_HANDLE },
      { path: "apps/:id/publish", element: <LegacyWorkspaceAppRedirectRoute />, handle: WORKSPACE_DEVELOP_ROUTE_HANDLE },
      { path: "agents/:id", element: <WorkspaceAgentDetailRoute />, handle: WORKSPACE_DEVELOP_ROUTE_HANDLE },
      { path: "agents/:id/publish", element: <WorkspaceAgentPublishRoute />, handle: WORKSPACE_DEVELOP_ROUTE_HANDLE },
      { path: "workflows", element: <WorkspaceWorkflowRedirectRoute />, handle: WORKSPACE_WORKFLOW_ROUTE_HANDLE },
      { path: "workflows/:workflowId", element: <WorkspaceWorkflowRedirectRoute />, handle: WORKSPACE_WORKFLOW_ROUTE_HANDLE },
      { path: "chatflows", element: <WorkspaceChatflowRedirectRoute />, handle: WORKSPACE_CHATFLOW_ROUTE_HANDLE },
      { path: "chatflows/:workflowId", element: <WorkspaceChatflowRedirectRoute />, handle: WORKSPACE_CHATFLOW_ROUTE_HANDLE },
      { path: "publish/agent/:bot_id", element: <SpaceAgentPublishManageAliasRoute />, handle: WORKSPACE_DEVELOP_ROUTE_HANDLE },
      {
        path: "bot/:bot_id",
        element: (
          <Suspense fallback={<PageShell loading testId="coze-agent-layout-loading" />}>
            <CozeAgentLayout />
          </Suspense>
        ),
        handle: WORKSPACE_DEVELOP_ROUTE_HANDLE,
        children: [
          {
            index: true,
            element: <SpaceAgentEditorRoute />,
            handle: { ...WORKSPACE_DEVELOP_ROUTE_HANDLE, requireBotEditorInit: true, pageName: "bot", hasHeader: true } as AppRouteHandle
          },
          {
            path: "publish",
            element: <SpaceAgentPublishRoute />,
            handle: { ...WORKSPACE_DEVELOP_ROUTE_HANDLE, requireBotEditorInit: false, pageName: "publish", hasHeader: true } as AppRouteHandle
          },
          {
            path: "analysis",
            element: <SpaceBotAnalysisAliasRoute />,
            handle: WORKSPACE_DEVELOP_ROUTE_HANDLE
          }
        ]
      }
    ]
  },
  {
    path: "/console",
    element: <LegacyConsoleRedirectRoute />,
    handle: WORKSPACE_LIST_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />
  },
  {
    path: "/select-workspace",
    element: <LegacyConsoleRedirectRoute />,
    handle: WORKSPACE_LIST_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />
  },
  {
    path: "/workspace/:workspaceId/*",
    element: <LegacyWorkspaceRouteRedirect />,
    handle: WORKSPACE_SHELL_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />
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
    path: "/w/:workspaceId/apps/:id",
    element: <LegacyWorkspaceAppRedirectRoute />,
    handle: WORKSPACE_DEVELOP_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />
  },
  {
    path: "/w/:workspaceId/apps/:id/publish",
    element: <LegacyWorkspaceAppRedirectRoute />,
    handle: WORKSPACE_DEVELOP_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />
  },
  {
    path: "/apps/:appKey/studio/apps/:id",
    element: <LegacyWorkspaceAppRedirectRoute />,
    handle: WORKSPACE_DEVELOP_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />
  },
  {
    path: "/apps/:appKey/studio/apps/:id/publish",
    element: <LegacyWorkspaceAppRedirectRoute />,
    handle: WORKSPACE_DEVELOP_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />
  },
  {
    path: "/apps/lowcode/:id/studio",
    element: <EditorShellLayout />,
    handle: WORKSPACE_DEVELOP_ROUTE_HANDLE,
    errorElement: <FatalErrorPage />,
    children: [{ index: true, element: <CanonicalLowcodeStudioRoute /> }]
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

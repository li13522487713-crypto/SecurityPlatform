import { useCallback, useEffect, useMemo, useState } from "react";
import { Button, Empty, Input, Modal, Spin, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import type {
  CanvasSchema,
  NodeTypeMetadata,
  WorkflowDetailResponse,
  WorkflowVersionItem
} from "@atlas/workflow-core-react";
import {
  WorkflowEditor,
  type CanvasValidationResult,
  type WorkflowPanelCommand,
  type WorkflowPanelCommandType
} from "@atlas/workflow-editor-react";
import {
  buildResourceSidebarSections,
  ensureWorkflowTab,
  type WorkflowSidebarAction,
  type WorkflowSidebarItem,
  type WorkflowSidebarSection
} from "./coze-adapter";
import { getWorkflowModuleCopy } from "./copy";
import type {
  AgentSummaryItem,
  AiDatabaseDetail,
  AiDatabaseListItem,
  AiDatabaseMutationRequest,
  AiPluginDetail,
  AiPluginListItem,
  AiPluginMutationRequest,
  AiSystemVariableDefinition,
  AiVariableListItem,
  AiVariableMutationRequest,
  ConversationListItem,
  ConversationMessageItem,
  KnowledgeBaseListItem,
  KnowledgeBaseMutationRequest,
  ResourceIdeLibraryItem,
  ResourceIdeLibraryType,
  ResourceIdeTab,
  WorkflowDependencies,
  WorkflowProblemItem,
  WorkflowTraceStepSummary,
  WorkflowListItem,
  WorkflowPageProps,
  WorkflowResourceMode
} from "./types";

interface WorkflowEditorShellProps extends WorkflowPageProps {
  workflowId: string;
  onBack: () => void;
  backPath?: string;
  mode?: WorkflowResourceMode;
}

interface WorkflowProcessSnapshot {
  status?: number;
}

type SidebarTabKey = "resources" | "references";
type WorkspaceViewKey = "logic" | "ui";
type CreateDialogKind = "workflow" | "chatflow" | "plugin" | "knowledge-base" | "database";
type GroupMenuKey = "workflow" | "plugin" | "data";
type ResourceContextMenuState = {
  itemKey: string;
  x: number;
  y: number;
  itemName: string;
  action: WorkflowSidebarAction;
  resourceType: ResourceIdeLibraryType | "workflow" | "chatflow" | "variables" | "conversations";
  resourceId?: string;
} | null;
type VariableDialogState = {
  visible: boolean;
  editingId?: number;
  key: string;
  value: string;
  scope: 0 | 1 | 2;
  scopeId: string;
  submitting: boolean;
  error?: string;
};
type ConversationDialogState = {
  visible: boolean;
  title: string;
  agentId: string;
  submitting: boolean;
  error?: string;
};

const DEFAULT_DATABASE_SCHEMA = JSON.stringify([{ name: "id" }, { name: "value" }], null, 2);
const VARIABLE_KEY_REGEX = /^(?!\d)[A-Za-z0-9$_]+$/;

function safeParseCanvas(canvasJson?: string): CanvasSchema {
  if (!canvasJson) {
    return { nodes: [], connections: [] };
  }

  try {
    const parsed = JSON.parse(canvasJson) as Partial<CanvasSchema>;
    return {
      nodes: Array.isArray(parsed.nodes) ? parsed.nodes : [],
      connections: Array.isArray(parsed.connections) ? parsed.connections : [],
      globals: parsed.globals,
      viewport: parsed.viewport,
      schemaVersion: parsed.schemaVersion
    };
  } catch {
    return { nodes: [], connections: [] };
  }
}

function buildEditorPath(workflowId: string, mode: WorkflowResourceMode): string {
  const segment = mode === "chatflow" ? "chat_flow" : "work_flow";
  return `${window.location.pathname.replace(/\/(work_flow|chat_flow)\/[^/]+\/editor$/, `/${segment}/${encodeURIComponent(workflowId)}/editor`)}${window.location.search}`;
}

function formatDateTime(value: string | undefined, locale: "zh-CN" | "en-US"): string {
  if (!value) {
    return "-";
  }
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString(locale);
}

const EXPLORE_CREATED_TEMPLATE_STORAGE_KEY = "atlas_explore_created_templates";

interface ExploreCreatedTemplateState {
  route: string;
  workflowId: string;
  mode: "workflow" | "chatflow";
  templateId: number;
  templateName: string;
  createdAt: string;
}

function readExploreCreatedTemplateStateMap(): Record<string, ExploreCreatedTemplateState> {
  if (typeof window === "undefined") {
    return {};
  }

  const raw = window.localStorage.getItem(EXPLORE_CREATED_TEMPLATE_STORAGE_KEY);
  if (!raw) {
    return {};
  }

  try {
    const parsed = JSON.parse(raw) as Record<string, ExploreCreatedTemplateState | string>;
    return Object.fromEntries(
      Object.entries(parsed).map(([key, value]) => {
        if (typeof value === "string") {
          return [key, {
            route: value,
            workflowId: "",
            mode: "workflow",
            templateId: Number(key),
            templateName: "",
            createdAt: ""
          } satisfies ExploreCreatedTemplateState];
        }

        return [key, value];
      })
    );
  } catch {
    return {};
  }
}

function readSearchParam(name: string): string {
  if (typeof window === "undefined") {
    return "";
  }
  return new URLSearchParams(window.location.search).get(name) ?? "";
}

function restoreTabFromKey(key: string): ResourceIdeTab | null {
  if (!key) {
    return null;
  }

  if (key === "problems" || key === "trace-list" || key === "trace-step" || key === "references" || key === "variables" || key === "conversations") {
    return {
      key,
      kind: key as ResourceIdeTab["kind"],
      title: key,
      closable: true
    };
  }

  const [kind, resourceId] = key.split("-", 2);
  if (!resourceId) {
    return null;
  }

  if (kind === "plugin") {
    return { key, kind: "plugin", resourceId, title: resourceId, closable: true };
  }
  if (kind === "knowledge") {
    return { key, kind: "knowledge", resourceId, title: resourceId, closable: true };
  }
  if (kind === "database") {
    return { key, kind: "database", resourceId, title: resourceId, closable: true };
  }

  return null;
}

export function WorkflowEditorShell({
  api,
  locale,
  workflowId,
  onBack,
  backPath,
  mode = "workflow"
}: WorkflowEditorShellProps) {
  const copy = getWorkflowModuleCopy(locale);
  const chatflowRoleLabel = locale === "zh-CN" ? "角色" : "Role";
  const apiClient = api.apiClient;
  const workflowTabKey = `${mode}-${workflowId}`;

  const [loading, setLoading] = useState(true);
  const [detail, setDetail] = useState<WorkflowDetailResponse | null>(null);
  const [versions, setVersions] = useState<WorkflowVersionItem[]>([]);
  const [nodeTypes, setNodeTypes] = useState<NodeTypeMetadata[]>([]);
  const [workflowItems, setWorkflowItems] = useState<WorkflowListItem[]>([]);
  const [pluginItems, setPluginItems] = useState<AiPluginListItem[]>([]);
  const [knowledgeItems, setKnowledgeItems] = useState<KnowledgeBaseListItem[]>([]);
  const [databaseItems, setDatabaseItems] = useState<AiDatabaseListItem[]>([]);
  const [conversationItems, setConversationItems] = useState<ConversationListItem[]>([]);
  const [sidebarTab, setSidebarTab] = useState<SidebarTabKey>(() => readSearchParam("sidebarTab") === "references" ? "references" : "resources");
  const [workspaceView, setWorkspaceView] = useState<WorkspaceViewKey>(() => readSearchParam("workspaceView") === "ui" ? "ui" : "logic");
  const [sidebarKeyword, setSidebarKeyword] = useState("");
  const [panelCommand, setPanelCommand] = useState<WorkflowPanelCommand | undefined>(undefined);
  const [commandNonce, setCommandNonce] = useState(0);
  const [tabs, setTabs] = useState<ResourceIdeTab[]>([
    {
      key: workflowTabKey,
      kind: mode === "chatflow" ? "chatflow-editor" : "workflow-editor",
      resourceId: workflowId,
      title: workflowId,
      closable: false,
      mode
    },
    ...((readSearchParam("openedTabs")
      .split(",")
      .map(item => item.trim())
      .filter(Boolean)
      .map(restoreTabFromKey)
      .filter((item): item is ResourceIdeTab => item !== null))
      .filter(item => item.key !== workflowTabKey))
  ]);
  const [activeTabKey, setActiveTabKey] = useState(() => readSearchParam("activeTab") || workflowTabKey);
  const [groupMenu, setGroupMenu] = useState<GroupMenuKey | null>(null);
  const [createDialog, setCreateDialog] = useState({
    visible: false,
    kind: "workflow" as CreateDialogKind,
    name: "",
    description: "",
    category: "",
    knowledgeType: 0 as 0 | 1 | 2,
    tableSchema: DEFAULT_DATABASE_SCHEMA,
    submitting: false,
    error: ""
  });
  const [libraryDialog, setLibraryDialog] = useState({
    visible: false,
    resourceType: null as ResourceIdeLibraryType | null,
    keyword: "",
    selectedId: "",
    loading: false,
    items: [] as ResourceIdeLibraryItem[]
  });
  const [pluginDetails, setPluginDetails] = useState<Record<string, AiPluginDetail>>({});
  const [pluginDrafts, setPluginDrafts] = useState<Record<string, AiPluginMutationRequest>>({});
  const [knowledgeDrafts, setKnowledgeDrafts] = useState<Record<string, KnowledgeBaseMutationRequest>>({});
  const [databaseDetails, setDatabaseDetails] = useState<Record<string, AiDatabaseDetail>>({});
  const [databaseDrafts, setDatabaseDrafts] = useState<Record<string, AiDatabaseMutationRequest>>({});
  const [variables, setVariables] = useState<AiVariableListItem[]>([]);
  const [systemVariables, setSystemVariables] = useState<AiSystemVariableDefinition[]>([]);
  const [variableDialog, setVariableDialog] = useState<VariableDialogState>({
    visible: false,
    key: "",
    value: "",
    scope: 0,
    scopeId: "",
    submitting: false
  });
  const [conversationDialog, setConversationDialog] = useState<ConversationDialogState>({
    visible: false,
    title: "",
    agentId: "",
    submitting: false
  });
  const [conversationMessages, setConversationMessages] = useState<Record<string, ConversationMessageItem[]>>({});
  const [selectedConversationId, setSelectedConversationId] = useState("");
  const [conversationDraft, setConversationDraft] = useState("");
  const [agents, setAgents] = useState<AgentSummaryItem[]>([]);
  const [processSnapshot, setProcessSnapshot] = useState<WorkflowProcessSnapshot | null>(null);
  const [dependencies, setDependencies] = useState<WorkflowDependencies | null>(null);
  const [canvasValidation, setCanvasValidation] = useState<CanvasValidationResult | null>(null);
  const [traceSteps, setTraceSteps] = useState<WorkflowTraceStepSummary[]>([]);
  const [selectedTraceStep, setSelectedTraceStep] = useState<WorkflowTraceStepSummary | null>(null);
  const [focusNodeKey, setFocusNodeKey] = useState("");
  const [highlightVariableKey, setHighlightVariableKey] = useState(readSearchParam("variableKey"));
  const [resourceContextMenu, setResourceContextMenu] = useState<ResourceContextMenuState>(null);
  const [templateSources, setTemplateSources] = useState<Record<string, ExploreCreatedTemplateState>>(() => readExploreCreatedTemplateStateMap());

  const canvas = useMemo(() => safeParseCanvas(detail?.canvasJson), [detail?.canvasJson]);
  const activeTab = useMemo(
    () => tabs.find(tab => tab.key === activeTabKey) ?? tabs[0],
    [activeTabKey, tabs]
  );
  const isWorkflowTab = activeTab?.kind === "workflow-editor" || activeTab?.kind === "chatflow-editor";
  const templateSource = useMemo(
    () => Object.values(templateSources).find(source => source.workflowId === workflowId),
    [templateSources, workflowId]
  );

  const loadContext = useCallback(async (keyword = "") => {
    setLoading(true);
    try {
      const [detailResponse, versionsResponse, nodeTypesResponse, workflowResult, pluginResult, knowledgeResult, databaseResult, conversationResult, dependencyResult] = await Promise.all([
        apiClient.getDetail?.(workflowId),
        apiClient.getVersions?.(workflowId),
        apiClient.getNodeTypes?.(),
        api.listWorkflows({ pageIndex: 1, pageSize: 20, keyword, mode, status: "all" }),
        api.listPlugins({ pageIndex: 1, pageSize: 20 }, keyword),
        api.listKnowledgeBases({ pageIndex: 1, pageSize: 20 }, keyword),
        api.listDatabases({ pageIndex: 1, pageSize: 20 }, keyword),
        api.listConversations({ pageIndex: 1, pageSize: 20 }),
        api.getDependencies(workflowId)
      ]);

      setDetail(detailResponse?.data ?? null);
      setVersions(versionsResponse?.data ?? []);
      setNodeTypes(nodeTypesResponse?.data ?? []);
      setWorkflowItems(workflowResult.items);
      setPluginItems(pluginResult.items);
      setKnowledgeItems(knowledgeResult.items);
      setDatabaseItems(databaseResult.items);
      setConversationItems(conversationResult.items);
      setDependencies(dependencyResult);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : copy.loadFailure);
    } finally {
      setLoading(false);
    }
  }, [api, apiClient, copy.loadFailure, mode, workflowId]);

  useEffect(() => {
    void loadContext();
  }, [loadContext]);

  useEffect(() => {
    setTemplateSources(readExploreCreatedTemplateStateMap());
  }, [workflowId, detail?.name]);

  useEffect(() => {
    const handle = window.setTimeout(() => {
      void loadContext(sidebarKeyword);
    }, 240);
    return () => {
      window.clearTimeout(handle);
    };
  }, [loadContext, sidebarKeyword]);

  useEffect(() => {
    setTabs(prev => ensureWorkflowTab(prev, workflowId, detail?.name ?? workflowId, mode));
  }, [detail?.name, mode, workflowId, workflowTabKey]);

  useEffect(() => {
    setTabs(prev => prev.map(tab => {
      if (tab.kind === "plugin" && tab.resourceId) {
        const item = pluginItems.find(current => String(current.id) === tab.resourceId);
        return item ? { ...tab, title: item.name } : tab;
      }
      if (tab.kind === "knowledge" && tab.resourceId) {
        const item = knowledgeItems.find(current => String(current.id) === tab.resourceId);
        return item ? { ...tab, title: item.name } : tab;
      }
      if (tab.kind === "database" && tab.resourceId) {
        const item = databaseItems.find(current => String(current.id) === tab.resourceId);
        return item ? { ...tab, title: item.name } : tab;
      }
      if (tab.kind === "variables") {
        return { ...tab, title: copy.variablesLabel };
      }
      if (tab.kind === "conversations") {
        return { ...tab, title: copy.conversationManagement };
      }
      if (tab.kind === "problems") {
        return { ...tab, title: copy.problemsLabel };
      }
      if (tab.kind === "trace-list") {
        return { ...tab, title: copy.traceLabel };
      }
      if (tab.kind === "references") {
        return { ...tab, title: copy.referencesTitle };
      }
      return tab;
    }));
  }, [copy.conversationManagement, copy.problemsLabel, copy.referencesTitle, copy.traceLabel, copy.variablesLabel, databaseItems, knowledgeItems, pluginItems]);

  useEffect(() => {
    if (tabs.some(tab => tab.key === activeTabKey)) {
      return;
    }
    setActiveTabKey(workflowTabKey);
  }, [activeTabKey, tabs, workflowTabKey]);

  useEffect(() => {
    const resourceType = libraryDialog.resourceType;
    if (!libraryDialog.visible || !resourceType) {
      return;
    }
    const handle = window.setTimeout(() => {
      void (async () => {
        setLibraryDialog(prev => ({ ...prev, loading: true }));
        try {
          const result = await api.listLibrary({ pageIndex: 1, pageSize: 20, keyword: libraryDialog.keyword }, resourceType);
          setLibraryDialog(prev => ({
            ...prev,
            items: result.items,
            loading: false,
            selectedId: result.items[0] ? String(result.items[0].resourceId) : ""
          }));
        } catch (error) {
          setLibraryDialog(prev => ({ ...prev, loading: false }));
          Toast.error(error instanceof Error ? error.message : copy.libraryLoadFailure);
        }
      })();
    }, 200);
    return () => {
      window.clearTimeout(handle);
    };
  }, [api, copy.libraryLoadFailure, libraryDialog.keyword, libraryDialog.resourceType, libraryDialog.visible]);

  useEffect(() => {
    if (activeTab?.kind === "plugin" && activeTab.resourceId && !pluginDetails[activeTab.resourceId]) {
      void (async () => {
        try {
          const item = await api.getPluginDetail(Number(activeTab.resourceId));
          setPluginDetails(prev => ({ ...prev, [activeTab.resourceId!]: item }));
          setPluginDrafts(prev => ({
            ...prev,
            [activeTab.resourceId!]: {
              name: item.name,
              description: item.description,
              icon: item.icon,
              category: item.category,
              type: item.type,
              sourceType: item.sourceType,
              authType: item.authType,
              definitionJson: item.definitionJson,
              authConfigJson: item.authConfigJson,
              toolSchemaJson: item.toolSchemaJson,
              openApiSpecJson: item.openApiSpecJson
            }
          }));
        } catch (error) {
          Toast.error(error instanceof Error ? error.message : copy.pluginLoadFailure);
        }
      })();
    }
    if (activeTab?.kind === "knowledge" && activeTab.resourceId && !knowledgeDrafts[activeTab.resourceId]) {
      void api.getKnowledgeBase(Number(activeTab.resourceId)).then(item => {
        setKnowledgeDrafts(prev => ({ ...prev, [activeTab.resourceId!]: { name: item.name, description: item.description, type: item.type } }));
      }).catch(error => {
        Toast.error(error instanceof Error ? error.message : copy.knowledgeLoadFailure);
      });
    }
    if (activeTab?.kind === "database" && activeTab.resourceId && !databaseDetails[activeTab.resourceId]) {
      void api.getDatabaseDetail(Number(activeTab.resourceId)).then(item => {
        setDatabaseDetails(prev => ({ ...prev, [activeTab.resourceId!]: item }));
        setDatabaseDrafts(prev => ({ ...prev, [activeTab.resourceId!]: { name: item.name, description: item.description, botId: item.botId, tableSchema: item.tableSchema } }));
      }).catch(error => {
        Toast.error(error instanceof Error ? error.message : copy.databaseLoadFailure);
      });
    }
    if (activeTab?.kind === "variables" && variables.length === 0) {
      void Promise.all([
        api.listVariables({ pageIndex: 1, pageSize: 20 }),
        api.listSystemVariables()
      ]).then(([listResult, systemResult]) => {
        setVariables(listResult.items);
        setSystemVariables(systemResult);
      }).catch(error => {
        Toast.error(error instanceof Error ? error.message : copy.variableLoadFailure);
      });
    }
    if (activeTab?.kind === "conversations" && agents.length === 0) {
      void api.listAgents({ pageIndex: 1, pageSize: 20 }).then(result => {
        setAgents(result.items);
      }).catch(error => {
        Toast.error(error instanceof Error ? error.message : copy.agentLoadFailure);
      });
    }
  }, [
    activeTab,
    agents.length,
    api,
    copy.agentLoadFailure,
    copy.databaseLoadFailure,
    copy.knowledgeLoadFailure,
    copy.pluginLoadFailure,
    copy.variableLoadFailure,
    databaseDetails,
    knowledgeDrafts,
    pluginDetails,
    variables.length
  ]);

  useEffect(() => {
    if (activeTab?.kind !== "conversations") {
      return;
    }
    const nextConversationId = selectedConversationId || conversationItems[0]?.id;
    if (!nextConversationId || conversationMessages[nextConversationId]) {
      return;
    }
    setSelectedConversationId(nextConversationId);
    void api.listConversationMessages(nextConversationId).then(messages => {
      setConversationMessages(prev => ({ ...prev, [nextConversationId]: messages }));
    }).catch(error => {
      Toast.error(error instanceof Error ? error.message : copy.conversationLoadFailure);
    });
  }, [activeTab?.kind, api, conversationItems, conversationMessages, copy.conversationLoadFailure, selectedConversationId]);

  useEffect(() => {
    const handle = () => setResourceContextMenu(null);
    window.addEventListener("click", handle);
    return () => window.removeEventListener("click", handle);
  }, []);

  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    params.set("sidebarTab", sidebarTab);
    params.set("workspaceView", workspaceView);
    params.set("activeTab", activeTabKey);
    params.set("openedTabs", tabs.map(tab => tab.key).join(","));
    if (highlightVariableKey) {
      params.set("variableKey", highlightVariableKey);
    } else {
      params.delete("variableKey");
    }
    if (selectedTraceStep?.nodeKey) {
      params.set("traceNodeKey", selectedTraceStep.nodeKey);
    } else {
      params.delete("traceNodeKey");
    }
    const nextUrl = `${window.location.pathname}?${params.toString()}`;
    window.history.replaceState(null, "", nextUrl);
  }, [activeTabKey, highlightVariableKey, selectedTraceStep?.nodeKey, sidebarTab, tabs, workspaceView]);

  useEffect(() => {
    if (!focusNodeKey || activeTabKey !== workflowTabKey) {
      return;
    }

    const handle = window.setTimeout(() => setFocusNodeKey(""), 80);
    return () => window.clearTimeout(handle);
  }, [activeTabKey, focusNodeKey, workflowTabKey]);

  useEffect(() => {
    if (!highlightVariableKey || activeTab?.kind !== "variables") {
      return;
    }

    const handle = window.setTimeout(() => setHighlightVariableKey(""), 1200);
    return () => window.clearTimeout(handle);
  }, [activeTab?.kind, highlightVariableKey]);

  useEffect(() => {
    if (selectedTraceStep || traceSteps.length === 0) {
      return;
    }

    const traceNodeKey = readSearchParam("traceNodeKey");
    if (!traceNodeKey) {
      return;
    }

    const restored = traceSteps.find(item => item.nodeKey === traceNodeKey);
    if (restored) {
      setSelectedTraceStep(restored);
    }
  }, [selectedTraceStep, traceSteps]);

  const problemItems = useMemo<WorkflowProblemItem[]>(() => {
    const canvasIssues = (canvasValidation?.canvasIssues ?? []).map((issue: string, index: number) => ({
      key: `canvas-${index}`,
      level: "canvas" as const,
      label: issue
    }));
    const nodeIssues = (canvasValidation?.nodeResults ?? [])
      .filter((item: { nodeKey: string; issues: string[] }) => item.issues.length > 0)
      .flatMap(item =>
        item.issues.map((issue: string, index: number) => ({
          key: `${item.nodeKey}-${index}`,
          level: "node" as const,
          label: issue,
          nodeKey: item.nodeKey
        }))
      );

    const dependencyIssues = dependencies
      ? [
          ...dependencies.subWorkflows,
          ...dependencies.plugins,
          ...dependencies.knowledgeBases,
          ...dependencies.databases
        ]
          .filter(item => item.description?.includes("不存在") || item.description?.includes("删除"))
          .map((item, index) => ({
            key: `resource-${item.resourceType}-${item.resourceId}-${index}`,
            level: "resource" as const,
            label: item.description ?? `${item.resourceType} 依赖异常`,
            nodeKey: item.sourceNodeKeys?.[0],
            resourceType: item.resourceType,
            resourceId: item.resourceId,
            sourceNodeKeys: item.sourceNodeKeys
          }))
      : [];

    return [...canvasIssues, ...nodeIssues, ...dependencyIssues];
  }, [canvasValidation, dependencies]);

  function emitPanelCommand(type: WorkflowPanelCommandType) {
    const nonce = commandNonce + 1;
    setCommandNonce(nonce);
    setPanelCommand({ type, nonce });
  }

  function openTab(tab: ResourceIdeTab) {
    if (tab.kind === "workflow-editor" || tab.kind === "chatflow-editor") {
      if (!tab.resourceId) {
        return;
      }
      window.location.assign(buildEditorPath(tab.resourceId, tab.kind === "chatflow-editor" ? "chatflow" : "workflow"));
      return;
    }
    setTabs(prev => prev.some(item => item.key === tab.key) ? prev : [...prev, tab]);
    setActiveTabKey(tab.key);
  }

  function focusSourceNode(sourceNodeKeys?: string[]) {
    if (!sourceNodeKeys || sourceNodeKeys.length === 0) {
      return;
    }

    setFocusNodeKey(sourceNodeKeys[0]);
    setActiveTabKey(workflowTabKey);
  }

  function openDependencyResource(item: {
    resourceType: string;
    resourceId: string;
    name: string;
    sourceNodeKeys?: string[];
  }) {
    if (item.resourceType === "workflow") {
      window.location.assign(buildEditorPath(item.resourceId, "workflow"));
      return;
    }

    if (item.resourceType === "variable") {
      setHighlightVariableKey(item.resourceId);
      openTab({
        key: "variables",
        kind: "variables",
        title: copy.variablesLabel,
        closable: true
      });
      return;
    }

    if (item.resourceType === "conversation") {
      openTab({
        key: "conversations",
        kind: "conversations",
        title: copy.conversationManagement,
        closable: true
      });
      if (conversationItems.some(conversation => conversation.id === item.resourceId)) {
        setSelectedConversationId(item.resourceId);
        return;
      }

      focusSourceNode(item.sourceNodeKeys);
      return;
    }

    const tabKind: ResourceIdeTab["kind"] = item.resourceType === "plugin"
      ? "plugin"
      : item.resourceType === "knowledge-base"
        ? "knowledge"
        : "database";
    openTab({
      key: `${tabKind}-${item.resourceId}`,
      kind: tabKind,
      resourceId: item.resourceId,
      title: item.name,
      closable: true
    });
  }

  function closeTab(tabKey: string) {
    const target = tabs.find(tab => tab.key === tabKey);
    if (!target || !target.closable) {
      return;
    }
    setTabs(prev => prev.filter(tab => tab.key !== tabKey));
    if (activeTabKey === tabKey) {
      setActiveTabKey(workflowTabKey);
    }
  }

  function openCreateDialog(kind: CreateDialogKind) {
    setCreateDialog({
      visible: true,
      kind,
      name: "",
      description: "",
      category: "",
      knowledgeType: 0,
      tableSchema: DEFAULT_DATABASE_SCHEMA,
      submitting: false,
      error: ""
    });
    setGroupMenu(null);
  }

  function openLibraryDialog(resourceType: ResourceIdeLibraryType) {
    setLibraryDialog({
      visible: true,
      resourceType,
      keyword: "",
      selectedId: "",
      loading: true,
      items: []
    });
    setGroupMenu(null);
  }

  async function refreshVariables() {
    const [listResult, systemResult] = await Promise.all([
      api.listVariables({ pageIndex: 1, pageSize: 20 }),
      api.listSystemVariables()
    ]);
    setVariables(listResult.items);
    setSystemVariables(systemResult);
  }

  async function refreshConversations() {
    const result = await api.listConversations({ pageIndex: 1, pageSize: 20 });
    setConversationItems(result.items);
    setSelectedConversationId(prev => {
      if (prev && result.items.some(item => item.id === prev)) {
        return prev;
      }

      return result.items[0]?.id ?? "";
    });
  }

  async function handleSelectConversation(conversationId: string) {
    setSelectedConversationId(conversationId);
    if (conversationMessages[conversationId]) {
      return;
    }

    try {
      const messages = await api.listConversationMessages(conversationId);
      setConversationMessages(prev => ({ ...prev, [conversationId]: messages }));
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : copy.conversationLoadFailure);
    }
  }

  async function handleDeletePlugin(resourceId: string) {
    try {
      await api.deletePlugin(Number(resourceId));
      setPluginDetails(prev => {
        const next = { ...prev };
        delete next[resourceId];
        return next;
      });
      setPluginDrafts(prev => {
        const next = { ...prev };
        delete next[resourceId];
        return next;
      });
      closeTab(`plugin-${resourceId}`);
      await loadContext(sidebarKeyword);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : copy.deleteLabel);
    }
  }

  async function handleDeleteKnowledge(resourceId: string) {
    try {
      await api.deleteKnowledgeBase(Number(resourceId));
      setKnowledgeDrafts(prev => {
        const next = { ...prev };
        delete next[resourceId];
        return next;
      });
      closeTab(`knowledge-${resourceId}`);
      await loadContext(sidebarKeyword);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : copy.deleteLabel);
    }
  }

  async function handleDeleteDatabase(resourceId: string) {
    try {
      await api.deleteDatabase(Number(resourceId));
      setDatabaseDetails(prev => {
        const next = { ...prev };
        delete next[resourceId];
        return next;
      });
      setDatabaseDrafts(prev => {
        const next = { ...prev };
        delete next[resourceId];
        return next;
      });
      closeTab(`database-${resourceId}`);
      await loadContext(sidebarKeyword);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : copy.deleteLabel);
    }
  }

  async function handleDeleteVariable(variableId: number) {
    try {
      await api.deleteVariable(variableId);
      await refreshVariables();
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : copy.deleteLabel);
    }
  }

  async function handleClearConversation(action: "context" | "history") {
    if (!selectedConversationId) {
      return;
    }

    try {
      if (action === "context") {
        await api.clearConversationContext(selectedConversationId);
      } else {
        await api.clearConversationHistory(selectedConversationId);
      }

      const messages = await api.listConversationMessages(selectedConversationId);
      setConversationMessages(prev => ({ ...prev, [selectedConversationId]: messages }));
      await refreshConversations();
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : copy.conversationLoadFailure);
    }
  }

  async function handleDeleteConversation(conversationId: string) {
    try {
      await api.deleteConversation(conversationId);
      setConversationMessages(prev => {
        const next = { ...prev };
        delete next[conversationId];
        return next;
      });
      if (selectedConversationId === conversationId) {
        setConversationDraft("");
      }
      await refreshConversations();
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : copy.deleteLabel);
    }
  }

  async function handleQuickTestRun() {
    if (!apiClient.runSync) {
      return;
    }
    try {
      const response = await apiClient.runSync(workflowId, { source: "draft", inputsJson: "{}" });
      if (!response.data?.executionId) {
        return;
      }
      setProcessSnapshot({ status: 2 });
      emitPanelCommand("openTestRun");
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : copy.testRunLabel);
    }
  }

  async function handleCreateSubmit() {
    const name = createDialog.name.trim();
    const description = createDialog.description.trim();
    if (!name) {
      setCreateDialog(prev => ({ ...prev, error: copy.requiredField }));
      return;
    }
    if ((createDialog.kind === "workflow" || createDialog.kind === "chatflow") && (name.length < 2 || name.length > 100)) {
      setCreateDialog(prev => ({ ...prev, error: copy.workflowNameValidation }));
      return;
    }
    if ((createDialog.kind === "workflow" || createDialog.kind === "chatflow") && !description) {
      setCreateDialog(prev => ({ ...prev, error: copy.requiredField }));
      return;
    }
    setCreateDialog(prev => ({ ...prev, submitting: true, error: "" }));
    try {
      if (createDialog.kind === "workflow" || createDialog.kind === "chatflow") {
        const id = await api.createWorkflow({ name, description, mode: createDialog.kind, createSource: "blank" });
        window.location.assign(buildEditorPath(id, createDialog.kind));
        return;
      }
      if (createDialog.kind === "plugin") {
        const id = await api.createPlugin({ name, description: description || undefined, category: createDialog.category.trim() || undefined, type: 0, sourceType: 0, authType: 0, definitionJson: "{}", authConfigJson: "{}", toolSchemaJson: "{}", openApiSpecJson: "" });
        await loadContext(sidebarKeyword);
        openTab({ key: `plugin-${id}`, kind: "plugin", resourceId: String(id), title: name, closable: true });
      } else if (createDialog.kind === "knowledge-base") {
        const id = await api.createKnowledgeBase({ name, description: description || undefined, type: createDialog.knowledgeType });
        await loadContext(sidebarKeyword);
        openTab({ key: `knowledge-${id}`, kind: "knowledge", resourceId: String(id), title: name, closable: true });
      } else {
        const validation = await api.validateDatabaseSchema(createDialog.tableSchema);
        if (!validation.isValid) {
          setCreateDialog(prev => ({ ...prev, submitting: false, error: validation.errors[0] ?? copy.databaseSchemaValidationFailed }));
          return;
        }
        const id = await api.createDatabase({ name, description: description || undefined, tableSchema: createDialog.tableSchema });
        await loadContext(sidebarKeyword);
        openTab({ key: `database-${id}`, kind: "database", resourceId: String(id), title: name, closable: true });
      }
      setCreateDialog(prev => ({ ...prev, visible: false, submitting: false }));
    } catch (error) {
      setCreateDialog(prev => ({ ...prev, submitting: false, error: error instanceof Error ? error.message : copy.createFailure("workflow") }));
    }
  }

  async function handleImportSubmit() {
    if (!libraryDialog.resourceType || !libraryDialog.selectedId) {
      Toast.warning(copy.librarySelectRequired);
      return;
    }
    try {
      const selected = libraryDialog.items.find(item => String(item.resourceId) === libraryDialog.selectedId);
      const result = await api.importLibraryItem({ resourceType: libraryDialog.resourceType, libraryItemId: Number(libraryDialog.selectedId) });
      setLibraryDialog(prev => ({ ...prev, visible: false }));
      await loadContext(sidebarKeyword);
      if (result.resourceType === "workflow") {
        const importedMode = selected?.resourceSubType === "chatflow" || selected?.path.includes("/chat_flow/") ? "chatflow" : "workflow";
        window.location.assign(buildEditorPath(String(result.resourceId), importedMode));
        return;
      }
      openTab({
        key: `${result.resourceType}-${result.resourceId}`,
        kind: result.resourceType === "plugin" ? "plugin" : result.resourceType === "knowledge-base" ? "knowledge" : "database",
        resourceId: String(result.resourceId),
        title: selected?.name ?? String(result.resourceId),
        closable: true
      });
      Toast.success(copy.libraryImportSuccess);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : copy.libraryImportFailure);
    }
  }

  function renderSidebar() {
    const sections: WorkflowSidebarSection[] = sidebarTab === "resources"
      ? buildResourceSidebarSections({
          copy,
          mode,
          currentWorkflowId: workflowId,
          workflowItems,
          pluginItems,
          knowledgeItems,
          databaseItems,
          conversations: conversationItems,
          keyword: sidebarKeyword
        })
      : [
          {
            key: "ref-subworkflows",
            title: "SubWorkflows",
            items: (dependencies?.subWorkflows ?? []).map<WorkflowSidebarItem>(item => ({
              key: `workflow-${item.resourceId}`,
              id: item.resourceId,
              resourceType: "workflow" as const,
              name: item.name,
              description: item.description,
              hint: item.sourceNodeKeys?.length ? `来源节点 ${item.sourceNodeKeys.length} 个` : item.description,
              action: { type: "route" as const, workflowId: item.resourceId, mode: "workflow" as const }
            }))
          },
          {
            key: "ref-plugins",
            title: copy.pluginLabel,
            items: (dependencies?.plugins ?? []).map<WorkflowSidebarItem>(item => ({
              key: `plugin-${item.resourceId}`,
              id: item.resourceId,
              resourceType: "plugin" as const,
              name: item.name,
              description: item.description,
              hint: item.sourceNodeKeys?.length ? `来源节点 ${item.sourceNodeKeys.length} 个` : item.description,
              action: {
                type: "tab" as const,
                tab: { key: `plugin-${item.resourceId}`, kind: "plugin" as const, resourceId: item.resourceId, title: item.name, closable: true }
              }
            }))
          },
          {
            key: "ref-data",
            title: copy.sectionData,
            items: [
              ...(dependencies?.knowledgeBases ?? []).map<WorkflowSidebarItem>(item => ({
                key: `knowledge-${item.resourceId}`,
                id: item.resourceId,
                resourceType: "knowledge-base" as const,
                name: item.name,
                description: item.description,
                hint: item.sourceNodeKeys?.length ? `来源节点 ${item.sourceNodeKeys.length} 个` : item.description,
                action: {
                  type: "tab" as const,
                  tab: { key: `knowledge-${item.resourceId}`, kind: "knowledge" as const, resourceId: item.resourceId, title: item.name, closable: true }
                }
              })),
              ...(dependencies?.databases ?? []).map<WorkflowSidebarItem>(item => ({
                key: `database-${item.resourceId}`,
                id: item.resourceId,
                resourceType: "database" as const,
                name: item.name,
                description: item.description,
                hint: item.sourceNodeKeys?.length ? `来源节点 ${item.sourceNodeKeys.length} 个` : item.description,
                action: {
                  type: "tab" as const,
                  tab: { key: `database-${item.resourceId}`, kind: "database" as const, resourceId: item.resourceId, title: item.name, closable: true }
                }
              }))
            ]
          },
          {
            key: "ref-variables",
            title: copy.variablesLabel,
            items: (dependencies?.variables ?? []).map<WorkflowSidebarItem>(item => ({
              key: `variable-${item.resourceId}`,
              id: item.resourceId,
              resourceType: "variables" as const,
              name: item.name,
              description: item.description,
              hint: item.sourceNodeKeys?.length ? `来源节点 ${item.sourceNodeKeys.length} 个` : item.description,
              action: {
                type: "tab" as const,
                tab: { key: "variables", kind: "variables" as const, title: copy.variablesLabel, closable: true }
              }
            }))
          },
          {
            key: "ref-conversations",
            title: copy.conversationManagement,
            items: (dependencies?.conversations ?? []).map<WorkflowSidebarItem>(item => ({
              key: `conversation-${item.resourceId}`,
              id: item.resourceId,
              resourceType: "conversations" as const,
              name: item.name,
              description: item.description,
              hint: item.sourceNodeKeys?.length ? `来源节点 ${item.sourceNodeKeys.length} 个` : item.description,
              action: {
                type: "tab" as const,
                tab: { key: "conversations", kind: "conversations" as const, title: copy.conversationManagement, closable: true }
              }
            }))
          }
        ];

    return sections.map(section => (
      <section key={section.key} className="module-workflow__coze-section">
        <div className="module-workflow__coze-section-head">
          <span>{section.title}</span>
          {sidebarTab === "resources" && (section.key === "workflow" || section.key === "plugin" || section.key === "data") ? (
            <div className="module-workflow__coze-group-actions">
              <button
                type="button"
                className="module-workflow__coze-plus"
                onClick={() => setGroupMenu(prev => prev === section.key ? null : section.key as GroupMenuKey)}
              >
                +
              </button>
              {groupMenu === section.key ? (
                <div className="module-workflow__coze-group-menu">
                  {section.key === "workflow" ? (
                    <>
                      <button type="button" onClick={() => openCreateDialog("workflow")}>{copy.menuCreateWorkflow}</button>
                      <button type="button" onClick={() => openCreateDialog("chatflow")}>{copy.menuCreateChatflow}</button>
                      <button type="button" onClick={() => openLibraryDialog("workflow")}>{copy.menuImportLibrary}</button>
                    </>
                  ) : null}
                  {section.key === "plugin" ? (
                    <>
                      <button type="button" onClick={() => openCreateDialog("plugin")}>{copy.menuCreatePlugin}</button>
                      <button type="button" onClick={() => openLibraryDialog("plugin")}>{copy.menuImportLibrary}</button>
                    </>
                  ) : null}
                  {section.key === "data" ? (
                    <>
                      <button type="button" onClick={() => openCreateDialog("knowledge-base")}>{copy.menuCreateKnowledge}</button>
                      <button type="button" onClick={() => openCreateDialog("database")}>{copy.menuCreateDatabase}</button>
                    </>
                  ) : null}
                </div>
              ) : null}
            </div>
          ) : null}
        </div>
        <div className="module-workflow__coze-section-items">
          {section.items.length === 0 && section.emptyText ? <div className="module-workflow__coze-empty">{section.emptyText}</div> : null}
          {section.items.map(item => (
            <button
              key={item.key}
              type="button"
              className={`module-workflow__coze-item${item.active || (item.action.type === "tab" && activeTabKey === item.action.tab.key) ? " is-active" : ""}`}
              onContextMenu={event => {
                event.preventDefault();
                setResourceContextMenu({
                  itemKey: item.key,
                  x: event.clientX,
                  y: event.clientY,
                  itemName: item.name,
                  action: item.action,
                  resourceType: item.resourceType === "knowledge-base" ? "knowledge-base" : item.resourceType,
                  resourceId: item.id
                });
              }}
              onClick={() => {
                if (sidebarTab === "references" && item.id) {
                  openDependencyResource({
                    resourceType: item.resourceType === "variables"
                      ? "variable"
                      : item.resourceType === "conversations"
                        ? "conversation"
                        : item.resourceType,
                    resourceId: item.id,
                    name: item.name,
                    sourceNodeKeys: dependencies?.subWorkflows.find(current => current.resourceId === item.id)?.sourceNodeKeys
                      ?? dependencies?.plugins.find(current => current.resourceId === item.id)?.sourceNodeKeys
                      ?? dependencies?.knowledgeBases.find(current => current.resourceId === item.id)?.sourceNodeKeys
                      ?? dependencies?.databases.find(current => current.resourceId === item.id)?.sourceNodeKeys
                      ?? dependencies?.variables.find(current => current.resourceId === item.id)?.sourceNodeKeys
                      ?? dependencies?.conversations.find(current => current.resourceId === item.id)?.sourceNodeKeys
                  });
                  return;
                }
                if (item.action.type === "route") {
                  window.location.assign(buildEditorPath(item.action.workflowId, item.action.mode));
                  return;
                }
                if (item.action.type === "tab") {
                  openTab(item.action.tab);
                }
              }}
            >
              <span className="module-workflow__coze-item-main">
                <strong>{item.name}</strong>
                {item.hint ? <small>{item.hint}</small> : null}
              </span>
              {item.status ? <span className="module-workflow__coze-item-status">{item.status}</span> : null}
              {item.badge ? <span className="module-workflow__coze-item-badge">{item.badge}</span> : null}
            </button>
          ))}
        </div>
      </section>
    ));
  }

  async function handleContextDelete() {
    if (!resourceContextMenu?.resourceId) {
      return;
    }

    try {
      if (resourceContextMenu.resourceType === "plugin") {
        await handleDeletePlugin(resourceContextMenu.resourceId);
      } else if (resourceContextMenu.resourceType === "knowledge-base") {
        await handleDeleteKnowledge(resourceContextMenu.resourceId);
      } else if (resourceContextMenu.resourceType === "database") {
        await handleDeleteDatabase(resourceContextMenu.resourceId);
      } else if (resourceContextMenu.resourceType === "workflow" || resourceContextMenu.resourceType === "chatflow") {
        await api.deleteWorkflow(resourceContextMenu.resourceId);
        await loadContext(sidebarKeyword);
      }
    } finally {
      setResourceContextMenu(null);
    }
  }

  async function handleContextExport() {
    if (!resourceContextMenu?.resourceId) {
      return;
    }

    const resourceType = resourceContextMenu.resourceType === "chatflow" ? "workflow" : resourceContextMenu.resourceType;
    if (resourceType === "variables" || resourceType === "conversations") {
      return;
    }

    try {
      await api.exportLibraryItem({ resourceType: resourceType as ResourceIdeLibraryType, resourceId: Number(resourceContextMenu.resourceId) });
      Toast.success(copy.libraryImportSuccess);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : copy.libraryImportFailure);
    } finally {
      setResourceContextMenu(null);
    }
  }

  async function handleContextMove() {
    if (!resourceContextMenu?.resourceId) {
      return;
    }

    const resourceType = resourceContextMenu.resourceType === "chatflow" ? "workflow" : resourceContextMenu.resourceType;
    if (resourceType === "variables" || resourceType === "conversations") {
      return;
    }

    try {
      await api.moveLibraryItem({ resourceType: resourceType as ResourceIdeLibraryType, resourceId: Number(resourceContextMenu.resourceId) });
      Toast.success(copy.libraryImportSuccess);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : copy.libraryImportFailure);
    } finally {
      setResourceContextMenu(null);
    }
  }

  function renderResourceTabs() {
    return (
      <div className="module-workflow__coze-resource-tabs">
        {tabs.map(tab => (
          <div key={tab.key} className={`module-workflow__coze-resource-tab${tab.key === activeTabKey ? " is-active" : ""}`}>
            <button type="button" onClick={() => openTab(tab)}>{tab.title}</button>
            {tab.closable ? <span role="button" tabIndex={0} onClick={() => closeTab(tab.key)}>×</span> : null}
          </div>
        ))}
      </div>
    );
  }

  function renderActivePanel() {
    if (workspaceView === "ui") {
      return <div className="module-workflow__coze-ui-placeholder"><Typography.Text>{copy.editorUiComingSoon}</Typography.Text></div>;
    }
    if (activeTab?.kind === "problems") {
      return (
        <div className="module-workflow__coze-resource-panel">
          <div className="module-workflow__coze-panel-toolbar">
            <Typography.Title heading={5} style={{ margin: 0 }}>{copy.problemsLabel}</Typography.Title>
            <Button theme="light" onClick={() => setActiveTabKey(workflowTabKey)}>{copy.openLabel}</Button>
          </div>
          {problemItems.length === 0 ? (
            <Empty title={copy.emptyReferences} image={null} />
          ) : (
            <div className="module-workflow__coze-list-card">
              {problemItems.map(item => (
                <div key={item.key} className="module-workflow__coze-list-row">
                  <div>
                    <strong>{item.level === "canvas" ? copy.problemsLabel : item.level === "resource" ? `${item.resourceType} / ${item.resourceId}` : item.nodeKey}</strong>
                    <small>{item.label}</small>
                  </div>
                  {item.level === "resource" && item.resourceType && item.resourceId ? (
                    <div className="module-workflow__coze-list-row-actions">
                      <button
                        type="button"
                        onClick={() => openDependencyResource({
                          resourceType: item.resourceType!,
                          resourceId: item.resourceId!,
                          name: item.resourceId!,
                          sourceNodeKeys: item.sourceNodeKeys
                        })}
                      >
                        {copy.openLabel}
                      </button>
                      {item.sourceNodeKeys?.length ? (
                        <button type="button" onClick={() => focusSourceNode(item.sourceNodeKeys)}>
                          定位来源
                        </button>
                      ) : null}
                    </div>
                  ) : item.nodeKey ? (
                    <div className="module-workflow__coze-list-row-actions">
                      <button
                        type="button"
                        onClick={() => {
                          setFocusNodeKey(item.nodeKey ?? "");
                          setActiveTabKey(workflowTabKey);
                        }}
                      >
                        {copy.openLabel}
                      </button>
                    </div>
                  ) : null}
                </div>
              ))}
            </div>
          )}
        </div>
      );
    }
    if (activeTab?.kind === "trace-list") {
      return (
        <div className="module-workflow__coze-resource-panel">
          <div className="module-workflow__coze-panel-toolbar">
            <Typography.Title heading={5} style={{ margin: 0 }}>{copy.traceLabel}</Typography.Title>
            <Button theme="light" onClick={() => setTraceSteps([])}>{copy.refreshLabel}</Button>
          </div>
          {traceSteps.length === 0 ? (
            <Empty title="暂无 Trace 记录" image={null} />
          ) : (
            <div className="module-workflow__coze-list-card">
              {traceSteps.map((step, index) => (
                <div key={`${step.timestamp}-${step.nodeKey}-${index}`} className="module-workflow__coze-list-row">
                  <div>
                    <strong>{step.nodeKey}</strong>
                    <small>{step.timestamp} / {step.status}</small>
                  </div>
                  <div className="module-workflow__coze-list-row-actions">
                    <button
                      type="button"
                      onClick={() => {
                        setSelectedTraceStep(step);
                        openTab({ key: "trace-step", kind: "trace-step", title: `${copy.traceLabel}: ${step.nodeKey}`, closable: true });
                      }}
                    >
                      详情
                    </button>
                    <button
                      type="button"
                      onClick={() => {
                        setFocusNodeKey(step.nodeKey);
                        setActiveTabKey(workflowTabKey);
                      }}
                    >
                      定位
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      );
    }
    if (activeTab?.kind === "trace-step") {
      return (
        <div className="module-workflow__coze-resource-panel">
          <div className="module-workflow__coze-panel-toolbar">
            <Typography.Title heading={5} style={{ margin: 0 }}>{copy.traceLabel}</Typography.Title>
            <Button theme="light" onClick={() => setActiveTabKey("trace-list")}>返回列表</Button>
          </div>
          {selectedTraceStep ? (
            <div className="module-workflow__coze-list-card">
              <div className="module-workflow__coze-list-row"><div><strong>节点</strong><small>{selectedTraceStep.nodeKey}</small></div></div>
              <div className="module-workflow__coze-list-row"><div><strong>状态</strong><small>{selectedTraceStep.status}</small></div></div>
              <div className="module-workflow__coze-list-row"><div><strong>时间</strong><small>{selectedTraceStep.timestamp}</small></div></div>
              <div className="module-workflow__coze-list-row"><div><strong>详情</strong><small>{selectedTraceStep.detail || "-"}</small></div></div>
              <div className="module-workflow__coze-list-row-actions">
                <button
                  type="button"
                  onClick={() => {
                    setFocusNodeKey(selectedTraceStep.nodeKey);
                    setActiveTabKey(workflowTabKey);
                  }}
                >
                  定位到画布
                </button>
              </div>
            </div>
          ) : (
            <Empty title="暂无 Trace 详情" image={null} />
          )}
        </div>
      );
    }
    if (activeTab?.kind === "references") {
      const dependencyItems = dependencies
        ? [
            ...dependencies.subWorkflows.map(item => ({ ...item, tabKind: item.resourceType === "workflow" ? "workflow-editor" : "workflow-editor" })),
            ...dependencies.plugins.map(item => ({ ...item, tabKind: "plugin" })),
            ...dependencies.knowledgeBases.map(item => ({ ...item, tabKind: "knowledge" })),
            ...dependencies.databases.map(item => ({ ...item, tabKind: "database" })),
            ...dependencies.variables.map(item => ({ ...item, tabKind: "variables" })),
            ...dependencies.conversations.map(item => ({ ...item, tabKind: "conversations" }))
          ]
        : [];
      return (
        <div className="module-workflow__coze-resource-panel">
          <div className="module-workflow__coze-panel-toolbar">
            <Typography.Title heading={5} style={{ margin: 0 }}>{copy.referencesTitle}</Typography.Title>
          </div>
          <div className="module-workflow__coze-list-card">
            {dependencyItems.length === 0 ? <Empty title={copy.emptyReferences} image={null} /> : dependencyItems.map(item => (
              <div key={`${item.resourceType}-${item.resourceId}`} className="module-workflow__coze-list-row">
                <div>
                  <strong>{item.name}</strong>
                  <small>{item.description || item.resourceType}{item.sourceNodeKeys?.length ? ` · 来源 ${item.sourceNodeKeys.length} 个节点` : ""}</small>
                </div>
                <div className="module-workflow__coze-list-row-actions">
                  <button
                    type="button"
                    onClick={() => openDependencyResource(item)}
                  >
                    {copy.openLabel}
                  </button>
                  {item.sourceNodeKeys?.length ? (
                    <button type="button" onClick={() => focusSourceNode(item.sourceNodeKeys)}>
                      定位来源
                    </button>
                  ) : null}
                </div>
              </div>
            ))}
          </div>
        </div>
      );
    }
    if (activeTab?.kind === "plugin" && activeTab.resourceId) {
      const detailResult = pluginDetails[activeTab.resourceId];
      const draft = pluginDrafts[activeTab.resourceId];
      if (!detailResult || !draft) {
        return <Spin />;
      }
      return (
        <div className="module-workflow__coze-resource-panel">
          <div className="module-workflow__coze-panel-toolbar">
            <Typography.Title heading={5} style={{ margin: 0 }}>{copy.pluginLabel}</Typography.Title>
            <div className="module-workflow__coze-panel-actions">
              <Button theme="light" onClick={() => void api.exportLibraryItem({ resourceType: "plugin", resourceId: Number(activeTab.resourceId) })}>{copy.libraryExportLabel}</Button>
              <Button theme="light" onClick={() => void api.moveLibraryItem({ resourceType: "plugin", resourceId: Number(activeTab.resourceId) })}>{copy.libraryMoveLabel}</Button>
              <Button onClick={() => void api.updatePlugin(Number(activeTab.resourceId), draft).then(() => loadContext(sidebarKeyword)).catch(error => Toast.error(error instanceof Error ? error.message : copy.pluginLoadFailure))}>{copy.saveLabel}</Button>
              <Button theme="solid" type="secondary" onClick={() => void api.publishPlugin(Number(activeTab.resourceId))}>{copy.publishLabel}</Button>
              <Button type="danger" theme="light" onClick={() => void handleDeletePlugin(activeTab.resourceId!)}>{copy.deleteLabel}</Button>
            </div>
          </div>
          <label className="module-workflow__coze-form-block"><span>{copy.nameLabel}</span><Input value={draft.name} onChange={value => setPluginDrafts(prev => ({ ...prev, [activeTab.resourceId!]: { ...prev[activeTab.resourceId!], name: value } }))} /></label>
          <label className="module-workflow__coze-form-block"><span>{copy.descriptionLabel}</span><textarea value={draft.description ?? ""} onChange={event => setPluginDrafts(prev => ({ ...prev, [activeTab.resourceId!]: { ...prev[activeTab.resourceId!], description: event.target.value } }))} rows={4} /></label>
          <div className="module-workflow__coze-metrics"><div><strong>{copy.pluginApiCountLabel}</strong><span>{detailResult.apis.length}</span></div><div><strong>{copy.updatedAtLabel}</strong><span>{formatDateTime(detailResult.updatedAt, locale)}</span></div></div>
        </div>
      );
    }
    if (activeTab?.kind === "knowledge" && activeTab.resourceId) {
      const draft = knowledgeDrafts[activeTab.resourceId];
      const info = knowledgeItems.find(item => String(item.id) === activeTab.resourceId);
      if (!draft || !info) {
        return <Spin />;
      }
      return (
        <div className="module-workflow__coze-resource-panel">
          <div className="module-workflow__coze-panel-toolbar">
            <Typography.Title heading={5} style={{ margin: 0 }}>{copy.knowledgeLabel}</Typography.Title>
            <div className="module-workflow__coze-panel-actions">
              <Button theme="light" onClick={() => void api.exportLibraryItem({ resourceType: "knowledge-base", resourceId: Number(activeTab.resourceId) })}>{copy.libraryExportLabel}</Button>
              <Button theme="light" onClick={() => void api.moveLibraryItem({ resourceType: "knowledge-base", resourceId: Number(activeTab.resourceId) })}>{copy.libraryMoveLabel}</Button>
              <Button onClick={() => void api.updateKnowledgeBase(Number(activeTab.resourceId), draft).then(() => loadContext(sidebarKeyword)).catch(error => Toast.error(error instanceof Error ? error.message : copy.knowledgeLoadFailure))}>{copy.saveLabel}</Button>
              <Button type="danger" theme="light" onClick={() => void handleDeleteKnowledge(activeTab.resourceId!)}>{copy.deleteLabel}</Button>
            </div>
          </div>
          <label className="module-workflow__coze-form-block"><span>{copy.nameLabel}</span><Input value={draft.name} onChange={value => setKnowledgeDrafts(prev => ({ ...prev, [activeTab.resourceId!]: { ...prev[activeTab.resourceId!], name: value } }))} /></label>
          <label className="module-workflow__coze-form-block"><span>{copy.descriptionLabel}</span><textarea value={draft.description ?? ""} onChange={event => setKnowledgeDrafts(prev => ({ ...prev, [activeTab.resourceId!]: { ...prev[activeTab.resourceId!], description: event.target.value } }))} rows={4} /></label>
          <div className="module-workflow__coze-metrics"><div><strong>{copy.documentCountLabel}</strong><span>{info.documentCount}</span></div><div><strong>{copy.chunkCountLabel}</strong><span>{info.chunkCount}</span></div></div>
        </div>
      );
    }
    if (activeTab?.kind === "database" && activeTab.resourceId) {
      const draft = databaseDrafts[activeTab.resourceId];
      const info = databaseDetails[activeTab.resourceId];
      if (!draft || !info) {
        return <Spin />;
      }
      return (
        <div className="module-workflow__coze-resource-panel">
          <div className="module-workflow__coze-panel-toolbar">
            <Typography.Title heading={5} style={{ margin: 0 }}>{copy.databaseLabel}</Typography.Title>
            <div className="module-workflow__coze-panel-actions">
              <Button theme="light" onClick={() => void api.exportLibraryItem({ resourceType: "database", resourceId: Number(activeTab.resourceId) })}>{copy.libraryExportLabel}</Button>
              <Button theme="light" onClick={() => void api.moveLibraryItem({ resourceType: "database", resourceId: Number(activeTab.resourceId) })}>{copy.libraryMoveLabel}</Button>
              <Button
                onClick={() => void api.validateDatabaseSchema(draft.tableSchema).then(result => {
                  if (!result.isValid) {
                    throw new Error(result.errors[0] ?? copy.databaseSchemaValidationFailed);
                  }
                  return api.updateDatabase(Number(activeTab.resourceId), draft);
                }).then(() => loadContext(sidebarKeyword)).catch(error => Toast.error(error instanceof Error ? error.message : copy.databaseLoadFailure))}
              >
                {copy.saveLabel}
              </Button>
              <Button type="danger" theme="light" onClick={() => void handleDeleteDatabase(activeTab.resourceId!)}>{copy.deleteLabel}</Button>
            </div>
          </div>
          <label className="module-workflow__coze-form-block"><span>{copy.nameLabel}</span><Input value={draft.name} onChange={value => setDatabaseDrafts(prev => ({ ...prev, [activeTab.resourceId!]: { ...prev[activeTab.resourceId!], name: value } }))} /></label>
          <label className="module-workflow__coze-form-block"><span>{copy.databaseSchemaLabel}</span><textarea value={draft.tableSchema} onChange={event => setDatabaseDrafts(prev => ({ ...prev, [activeTab.resourceId!]: { ...prev[activeTab.resourceId!], tableSchema: event.target.value } }))} rows={10} /></label>
          <div className="module-workflow__coze-metrics"><div><strong>{copy.databaseRecordCountLabel}</strong><span>{info.recordCount}</span></div></div>
        </div>
      );
    }
    if (activeTab?.kind === "variables") {
      return (
        <div className="module-workflow__coze-resource-panel">
          <div className="module-workflow__coze-panel-toolbar">
            <Typography.Title heading={5} style={{ margin: 0 }}>{copy.variablesLabel}</Typography.Title>
            <Button theme="solid" type="secondary" onClick={() => setVariableDialog({ visible: true, key: "", value: "", scope: 0, scopeId: "", submitting: false })}>{copy.variableCreateLabel}</Button>
          </div>
          <div className="module-workflow__coze-two-column">
            <div className="module-workflow__coze-list-card">{variables.map(item => <div key={item.id} className={`module-workflow__coze-list-row${highlightVariableKey === item.key ? " is-selected" : ""}`}><div><strong>{item.key}</strong><small>{item.value || copy.noDescription}</small></div><div className="module-workflow__coze-list-row-actions"><button type="button" onClick={() => setVariableDialog({ visible: true, editingId: item.id, key: item.key, value: item.value ?? "", scope: item.scope, scopeId: item.scopeId ? String(item.scopeId) : "", submitting: false })}>{copy.editLabel}</button><button type="button" onClick={() => void handleDeleteVariable(item.id)}>{copy.deleteLabel}</button></div></div>)}</div>
            <div className="module-workflow__coze-list-card">{systemVariables.map(item => <div key={item.key} className="module-workflow__coze-list-row"><div><strong>{item.key}</strong><small>{item.description}</small></div><Tag color="grey">{copy.systemVariableReadonly}</Tag></div>)}</div>
          </div>
        </div>
      );
    }
    if (activeTab?.kind === "conversations") {
      const messages = selectedConversationId ? (conversationMessages[selectedConversationId] ?? []) : [];
      return (
        <div className="module-workflow__coze-resource-panel">
          <div className="module-workflow__coze-panel-toolbar">
            <Typography.Title heading={5} style={{ margin: 0 }}>{copy.conversationManagement}</Typography.Title>
            <div className="module-workflow__coze-panel-actions">
              <Button theme="light" onClick={() => void handleClearConversation("context")}>{copy.clearContextLabel}</Button>
              <Button theme="light" onClick={() => void handleClearConversation("history")}>{copy.clearHistoryLabel}</Button>
              <Button theme="solid" type="secondary" onClick={() => setConversationDialog({ visible: true, title: "", agentId: agents[0]?.id ?? "", submitting: false })}>{copy.conversationCreateLabel}</Button>
            </div>
          </div>
          <div className="module-workflow__coze-two-column">
            <div className="module-workflow__coze-list-card">{conversationItems.map(item => <div key={item.id} className={`module-workflow__coze-list-row${selectedConversationId === item.id ? " is-selected" : ""}`}><button type="button" onClick={() => void handleSelectConversation(item.id)}><strong>{item.title || `${copy.conversationLabel} ${item.id}`}</strong><small>{item.messageCount} / {formatDateTime(item.lastMessageAt ?? item.createdAt, locale)}</small></button><div className="module-workflow__coze-list-row-actions"><button type="button" onClick={() => void handleDeleteConversation(item.id)}>{copy.deleteLabel}</button></div></div>)}</div>
            <div className="module-workflow__coze-list-card"><div className="module-workflow__coze-message-list">{messages.map(item => <div key={item.id} className="module-workflow__coze-message"><Tag color={item.role === "user" ? "blue" : "green"}>{item.role}</Tag><p>{item.content}</p></div>)}</div><div className="module-workflow__coze-message-compose"><textarea value={conversationDraft} onChange={event => setConversationDraft(event.target.value)} rows={4} /><Button theme="solid" type="secondary" onClick={() => { if (!selectedConversationId || !conversationDraft.trim()) { return; } void api.appendConversationMessage(selectedConversationId, { role: "user", content: conversationDraft.trim() }).then(() => api.listConversationMessages(selectedConversationId)).then(result => { setConversationMessages(prev => ({ ...prev, [selectedConversationId]: result })); setConversationDraft(""); }).catch(error => Toast.error(error instanceof Error ? error.message : copy.conversationLoadFailure)); }}>{copy.appendMessageLabel}</Button></div></div>
          </div>
        </div>
      );
    }
    return <WorkflowEditor workflowId={workflowId} apiClient={apiClient} locale={locale} mode={mode} panelCommand={panelCommand} focusNodeKey={focusNodeKey} onValidationChange={setCanvasValidation} onTraceStepsChange={setTraceSteps} onBack={() => { if (backPath) { window.location.assign(backPath); return; } onBack(); }} />;
  }

  return (
    <section className="module-workflow__coze-editor" data-testid={mode === "chatflow" ? "app-chatflow-editor-shell" : "app-workflow-editor-shell"}>
      <div className="module-workflow__coze-workspace-tabs">
        <button type="button" className={`module-workflow__coze-workspace-tab${workspaceView === "logic" ? " is-active" : ""}`} onClick={() => setWorkspaceView("logic")}>{copy.editorTabLogic}</button>
        <button type="button" className={`module-workflow__coze-workspace-tab${workspaceView === "ui" ? " is-active" : ""}`} onClick={() => setWorkspaceView("ui")}>{copy.editorTabUi}</button>
      </div>
      <div className="module-workflow__coze-layout">
        <aside className="module-workflow__coze-sidebar">
          <div className="module-workflow__coze-sidebar-top">
            <div className="module-workflow__coze-sidebar-tabs">
              <button type="button" className={sidebarTab === "resources" ? "is-active" : ""} onClick={() => setSidebarTab("resources")}>{copy.resourcesTab}</button>
              <button type="button" className={sidebarTab === "references" ? "is-active" : ""} onClick={() => setSidebarTab("references")}>{copy.referencesTab}</button>
            </div>
            {sidebarTab === "resources" ? <Input value={sidebarKeyword} onChange={setSidebarKeyword} placeholder={copy.sidebarSearchPlaceholder} showClear /> : null}
          </div>
          <div className="module-workflow__coze-sidebar-body">{loading ? <div className="module-workflow__coze-loading"><Spin /></div> : renderSidebar()}</div>
        </aside>
        <div className="module-workflow__coze-workspace">
          {renderResourceTabs()}
          <div className="module-workflow__coze-workspace-header">
            <div className="module-workflow__coze-workspace-chip"><span className="module-workflow__coze-workspace-dot" /><strong>{activeTab?.title ?? detail?.name ?? workflowId}</strong></div>
            {isWorkflowTab ? <div className="module-workflow__coze-workspace-actions"><Button theme="borderless" onClick={() => void loadContext(sidebarKeyword)}>{copy.refreshCanvasLabel}</Button><Button theme="borderless" onClick={() => emitPanelCommand("openNodePanel")}>{copy.addNodeLabel}</Button><Button theme="borderless" onClick={() => openTab({ key: "problems", kind: "problems", title: copy.problemsLabel, closable: true })}>{copy.problemsLabel}</Button><Button theme="borderless" onClick={() => openTab({ key: "trace-list", kind: "trace-list", title: copy.traceLabel, closable: true })}>{copy.traceLabel}</Button><Button theme="borderless" onClick={() => openTab({ key: "references", kind: "references", title: copy.referencesTitle, closable: true })}>{copy.referencesTab}</Button><Button theme="borderless" onClick={() => emitPanelCommand("openVariables")}>{copy.variablesLabel}</Button>{mode === "chatflow" ? <Button theme="borderless" onClick={() => emitPanelCommand("openRoleConfig")}>{chatflowRoleLabel}</Button> : null}<Button theme="light" type="tertiary" onClick={() => emitPanelCommand("openDebug")}>{copy.debugLabel}</Button><Button theme="solid" type="secondary" onClick={() => void handleQuickTestRun()}>{copy.testRunLabel}</Button></div> : null}
          </div>
          {isWorkflowTab && templateSource ? (
            <div className="module-workflow__coze-status-strip">
              <span><Tag color="green">来自模板市场</Tag></span>
              <span>模板：{templateSource.templateName || `模板#${templateSource.templateId}`}</span>
              <span>创建时间：{formatDateTime(templateSource.createdAt, locale)}</span>
            </div>
          ) : null}
          <div className="module-workflow__coze-editor-surface">{renderActivePanel()}</div>
          {isWorkflowTab ? <div className="module-workflow__coze-status-strip"><span>{copy.versionLabel}: v{detail?.latestVersionNumber ?? 0}</span><span>{copy.testRunLabel}: {processSnapshot?.status ? copy.publishedStatus : copy.draftStatus}</span><span>{copy.updatedAtLabel}: {formatDateTime(detail?.updatedAt, locale)}</span></div> : null}
        </div>
      </div>
      <Modal title={copy.createDialogTitle(createDialog.kind)} visible={createDialog.visible} onCancel={() => setCreateDialog(prev => ({ ...prev, visible: false, submitting: false, error: "" }))} okText={copy.createDialogConfirm(createDialog.kind)} confirmLoading={createDialog.submitting} onOk={() => void handleCreateSubmit()} className="module-workflow__coze-modal">
        <div className="module-workflow__coze-modal-body">
          <div className="module-workflow__coze-modal-icon">{copy.createDialogGlyph(createDialog.kind)}</div>
          <label className="module-workflow__coze-modal-field"><span>{copy.nameLabel}</span><Input value={createDialog.name} onChange={value => setCreateDialog(prev => ({ ...prev, name: value, error: "" }))} /></label>
          <label className="module-workflow__coze-modal-field"><span>{copy.descriptionLabel}</span><textarea value={createDialog.description} onChange={event => setCreateDialog(prev => ({ ...prev, description: event.target.value, error: "" }))} rows={createDialog.kind === "database" ? 4 : 6} /></label>
          {createDialog.kind === "plugin" ? <label className="module-workflow__coze-modal-field"><span>{copy.pluginCategoryLabel}</span><Input value={createDialog.category} onChange={value => setCreateDialog(prev => ({ ...prev, category: value }))} /></label> : null}
          {createDialog.kind === "knowledge-base" ? <label className="module-workflow__coze-modal-field"><span>{copy.knowledgeTypeLabel}</span><select className="module-workflow__coze-select" value={String(createDialog.knowledgeType)} onChange={event => setCreateDialog(prev => ({ ...prev, knowledgeType: Number(event.target.value) as 0 | 1 | 2 }))}><option value="0">{copy.knowledgeTypeTextLabel}</option><option value="1">{copy.knowledgeTypeTableLabel}</option><option value="2">{copy.knowledgeTypeImageLabel}</option></select></label> : null}
          {createDialog.kind === "database" ? <label className="module-workflow__coze-modal-field"><span>{copy.databaseSchemaLabel}</span><textarea value={createDialog.tableSchema} onChange={event => setCreateDialog(prev => ({ ...prev, tableSchema: event.target.value, error: "" }))} rows={10} /></label> : null}
          {createDialog.error ? <div className="module-workflow__coze-form-error">{createDialog.error}</div> : null}
        </div>
      </Modal>
      <Modal title={copy.libraryDialogTitle} visible={libraryDialog.visible} onCancel={() => setLibraryDialog(prev => ({ ...prev, visible: false }))} okText={copy.menuImportLibrary} onOk={() => void handleImportSubmit()} className="module-workflow__coze-modal">
        <div className="module-workflow__coze-modal-body">
          <Input value={libraryDialog.keyword} onChange={value => setLibraryDialog(prev => ({ ...prev, keyword: value }))} placeholder={copy.searchLibraryPlaceholder} />
          <div className="module-workflow__coze-library-list">{libraryDialog.loading ? <Spin /> : null}{libraryDialog.items.map(item => <label key={item.resourceId} className="module-workflow__coze-library-item"><input type="radio" checked={libraryDialog.selectedId === String(item.resourceId)} onChange={() => setLibraryDialog(prev => ({ ...prev, selectedId: String(item.resourceId) }))} /><div><strong>{item.name}</strong><small>{item.description || item.path}</small></div></label>)}</div>
        </div>
      </Modal>
      <Modal title={variableDialog.editingId ? copy.variableEditLabel : copy.variableCreateLabel} visible={variableDialog.visible} onCancel={() => setVariableDialog(prev => ({ ...prev, visible: false, submitting: false, error: undefined }))} okText={copy.saveLabel} confirmLoading={variableDialog.submitting} onOk={() => { const normalizedKey = variableDialog.key.trim(); if (!VARIABLE_KEY_REGEX.test(normalizedKey)) { setVariableDialog(prev => ({ ...prev, error: copy.variableKeyValidation })); return; } if ((variableDialog.scope === 1 || variableDialog.scope === 2) && (!variableDialog.scopeId.trim() || Number(variableDialog.scopeId) <= 0)) { setVariableDialog(prev => ({ ...prev, error: copy.requiredField })); return; } const request: AiVariableMutationRequest = { key: normalizedKey, value: variableDialog.value.trim() || undefined, scope: variableDialog.scope, scopeId: variableDialog.scope === 1 || variableDialog.scope === 2 ? Number(variableDialog.scopeId) : undefined }; setVariableDialog(prev => ({ ...prev, submitting: true, error: undefined })); const runner = variableDialog.editingId ? api.updateVariable(variableDialog.editingId, request) : api.createVariable(request); void Promise.resolve(runner).then(() => refreshVariables()).then(() => setVariableDialog({ visible: false, key: "", value: "", scope: 0, scopeId: "", submitting: false })).catch(error => setVariableDialog(prev => ({ ...prev, submitting: false, error: error instanceof Error ? error.message : copy.variableSaveFailure }))); }} className="module-workflow__coze-modal">
        <div className="module-workflow__coze-modal-body">
          <label className="module-workflow__coze-modal-field"><span>{copy.variableKeyLabel}</span><Input value={variableDialog.key} onChange={value => setVariableDialog(prev => ({ ...prev, key: value, error: undefined }))} /></label>
          <label className="module-workflow__coze-modal-field"><span>{copy.variableValueLabel}</span><textarea value={variableDialog.value} onChange={event => setVariableDialog(prev => ({ ...prev, value: event.target.value, error: undefined }))} rows={6} /></label>
          <label className="module-workflow__coze-modal-field"><span>{copy.variableScopeLabel}</span><select className="module-workflow__coze-select" value={String(variableDialog.scope)} onChange={event => setVariableDialog(prev => ({ ...prev, scope: Number(event.target.value) as 0 | 1 | 2, scopeId: Number(event.target.value) === 0 ? "" : prev.scopeId, error: undefined }))}><option value="0">{copy.variableScopeGlobalLabel}</option><option value="1">{copy.variableScopeProjectLabel}</option><option value="2">{copy.variableScopeBotLabel}</option></select></label>
          {variableDialog.scope === 1 || variableDialog.scope === 2 ? <label className="module-workflow__coze-modal-field"><span>{copy.variableScopeIdLabel}</span><Input value={variableDialog.scopeId} onChange={value => setVariableDialog(prev => ({ ...prev, scopeId: value, error: undefined }))} /></label> : null}
          {variableDialog.error ? <div className="module-workflow__coze-form-error">{variableDialog.error}</div> : null}
        </div>
      </Modal>
      <Modal title={copy.conversationCreateLabel} visible={conversationDialog.visible} onCancel={() => setConversationDialog(prev => ({ ...prev, visible: false, submitting: false, error: undefined }))} okText={copy.saveLabel} confirmLoading={conversationDialog.submitting} onOk={() => { if (!conversationDialog.agentId) { setConversationDialog(prev => ({ ...prev, error: copy.conversationAgentRequired })); return; } setConversationDialog(prev => ({ ...prev, submitting: true, error: undefined })); void api.createConversation({ agentId: conversationDialog.agentId, title: conversationDialog.title.trim() || undefined }).then(() => refreshConversations()).then(() => setConversationDialog({ visible: false, title: "", agentId: "", submitting: false })).catch(error => setConversationDialog(prev => ({ ...prev, submitting: false, error: error instanceof Error ? error.message : copy.conversationCreateFailure }))); }} className="module-workflow__coze-modal">
        <div className="module-workflow__coze-modal-body">
          <label className="module-workflow__coze-modal-field"><span>{copy.agentLabel}</span><select className="module-workflow__coze-select" value={conversationDialog.agentId} onChange={event => setConversationDialog(prev => ({ ...prev, agentId: event.target.value, error: undefined }))}><option value="">{copy.selectAgentPlaceholder}</option>{agents.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
          <label className="module-workflow__coze-modal-field"><span>{copy.conversationTitleLabel}</span><Input value={conversationDialog.title} onChange={value => setConversationDialog(prev => ({ ...prev, title: value, error: undefined }))} /></label>
          {conversationDialog.error ? <div className="module-workflow__coze-form-error">{conversationDialog.error}</div> : null}
        </div>
      </Modal>
      {resourceContextMenu ? (
        <div
          className="module-workflow__coze-context-menu"
          style={{ left: resourceContextMenu.x, top: resourceContextMenu.y }}
          onClick={event => event.stopPropagation()}
        >
          <button
            type="button"
            onClick={() => {
              if (resourceContextMenu.action.type === "route") {
                window.location.assign(buildEditorPath(resourceContextMenu.action.workflowId, resourceContextMenu.action.mode));
              } else if (resourceContextMenu.action.type === "tab") {
                openTab(resourceContextMenu.action.tab);
              }
              setResourceContextMenu(null);
            }}
          >
            {copy.openLabel}
          </button>
          <button
            type="button"
            onClick={() => {
              if (resourceContextMenu.action.type === "route") {
                window.open(buildEditorPath(resourceContextMenu.action.workflowId, resourceContextMenu.action.mode), "_blank", "noopener,noreferrer");
              } else if (resourceContextMenu.action.type === "tab") {
                openTab(resourceContextMenu.action.tab);
              }
              setResourceContextMenu(null);
            }}
          >
            新标签打开
          </button>
          {resourceContextMenu.resourceType !== "variables" && resourceContextMenu.resourceType !== "conversations" ? (
            <>
              <button type="button" onClick={() => void handleContextExport()}>{copy.libraryExportLabel}</button>
              <button type="button" onClick={() => void handleContextMove()}>{copy.libraryMoveLabel}</button>
            </>
          ) : null}
          {(resourceContextMenu.resourceType === "plugin" || resourceContextMenu.resourceType === "knowledge-base" || resourceContextMenu.resourceType === "database" || resourceContextMenu.resourceType === "workflow" || resourceContextMenu.resourceType === "chatflow") ? (
            <button type="button" className="is-danger" onClick={() => void handleContextDelete()}>{copy.deleteLabel}</button>
          ) : null}
        </div>
      ) : null}
    </section>
  );
}

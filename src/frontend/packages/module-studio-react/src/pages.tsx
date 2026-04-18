import { startTransition, useDeferredValue, useEffect, useMemo, useRef, useState } from "react";
import type { ReactNode } from "react";
import {
  Banner,
  Button,
  Descriptions,
  Dropdown,
  Empty,
  Input,
  InputNumber,
  Modal,
  Radio,
  Select,
  Space,
  Switch,
  Tag,
  Toast,
  Typography
} from "@douyinfe/semi-ui";
import { IconPlus } from "@douyinfe/semi-icons";
import {
  composeAgentPromptSections,
  EMPTY_AGENT_PROMPT_SECTIONS,
  isRecord
} from "./assistant/agent-ide-helpers";
import { AppBuilderPage } from "./app-builder";
import { ResourceReferenceCard } from "./shared/resource-reference-card";
import type {
  AgentListItem,
  AgentDetail,
  AgentDatabaseBindingInput,
  AgentKnowledgeBindingInput,
  AgentPluginBindingInput,
  AgentPluginToolBinding,
  AgentPluginParameterBinding,
  AgentVariableBindingInput,
  ChatMessageItem,
  ConversationItem,
  StudioApplicationCreateRequest,
  StudioApplicationConversationTemplate,
  StudioApplicationConversationTemplateCreateRequest,
  StudioApplicationPublishRecord,
  StudioApplicationSummary,
  StudioAssistantPublication,
  StudioDatabaseDetail,
  StudioDatabaseRecordItem,
  StudioKnowledgeBaseDetail,
  StudioSystemVariableDefinition,
  StudioVariableCreateRequest,
  StudioVariableItem,
  StudioWorkspaceOverview,
  DevelopFocus,
  DevelopResourceSummary,
  ModelConfigConnectionTestRequest,
  ModelConfigCreateRequest,
  ModelConfigItem,
  ModelConfigPromptTestRequest,
  ModelConfigStats,
  ModelConfigUpdateRequest,
  StudioLocale,
  WorkspaceIdeResource,
  WorkspaceIdeSummary,
  WorkbenchTrace,
  WorkflowListItem,
  StudioPageProps
} from "./types";

interface ModelConfigDraft {
  name: string;
  providerType: string;
  apiKey: string;
  baseUrl: string;
  defaultModel: string;
  modelId: string;
  systemPrompt: string;
  isEnabled: boolean;
  supportsEmbedding: boolean;
  enableStreaming: boolean;
  enableReasoning: boolean;
  enableTools: boolean;
  enableVision: boolean;
  enableJsonMode: boolean;
  temperature: number | undefined;
  maxTokens: number | undefined;
  topP: number | undefined;
  frequencyPenalty: number | undefined;
  presencePenalty: number | undefined;
}

const DEFAULT_MODEL_CONFIG_DRAFT: ModelConfigDraft = {
  name: "",
  providerType: "openai",
  apiKey: "",
  baseUrl: "",
  defaultModel: "",
  modelId: "",
  systemPrompt: "",
  isEnabled: true,
  supportsEmbedding: false,
  enableStreaming: true,
  enableReasoning: false,
  enableTools: false,
  enableVision: false,
  enableJsonMode: false,
  temperature: undefined,
  maxTokens: undefined,
  topP: undefined,
  frequencyPenalty: undefined,
  presencePenalty: undefined
};

const MODEL_PROVIDER_OPTIONS = [
  { label: "OpenAI", value: "openai" },
  { label: "DeepSeek", value: "deepseek" },
  { label: "Ollama", value: "ollama" },
  { label: "Custom", value: "custom" }
];

function Surface({
  title,
  subtitle,
  testId,
  toolbar,
  children
}: {
  title: string;
  subtitle: string;
  testId: string;
  toolbar?: ReactNode;
  children: ReactNode;
}) {
  return (
    <section className="module-studio__page" data-testid={testId}>
      <div className="module-studio__header">
        <div>
          <Typography.Title heading={4} style={{ margin: 0 }}>{title}</Typography.Title>
          <Typography.Text type="tertiary">{subtitle}</Typography.Text>
        </div>
        {toolbar ? <div className="module-studio__toolbar">{toolbar}</div> : null}
      </div>
      <div className="module-studio__surface">{children}</div>
    </section>
  );
}

function localizedText(locale: StudioLocale, zhCN: string, enUS: string): string {
  return locale === "en-US" ? enUS : zhCN;
}

function resolvePluginClassificationTag(
  sourceType?: number,
  hasMarketplaceSource = false
): { label: "内置" | "自定义" | "商业"; color: "cyan" | "blue" | "orange" } {
  if (hasMarketplaceSource) {
    return { label: "商业", color: "orange" };
  }

  if (sourceType === 2) {
    return { label: "内置", color: "cyan" };
  }

  return { label: "自定义", color: "blue" };
}

const EXPLORE_IMPORTED_PLUGIN_STORAGE_KEY = "atlas_explore_imported_plugins";

interface ExploreImportedPluginState {
  route: string;
  importedPluginId: number;
  sourceProductId: number;
  sourceName: string;
  importedAt: string;
}

function readExploreImportedPluginStateMap(): Record<string, ExploreImportedPluginState> {
  if (typeof window === "undefined") {
    return {};
  }

  const raw = window.localStorage.getItem(EXPLORE_IMPORTED_PLUGIN_STORAGE_KEY);
  if (!raw) {
    return {};
  }

  try {
    const parsed = JSON.parse(raw) as Record<string, ExploreImportedPluginState | string>;
    return Object.fromEntries(
      Object.entries(parsed).map(([key, value]) => {
        if (typeof value === "string") {
          return [key, {
            route: value,
            importedPluginId: 0,
            sourceProductId: Number(key),
            sourceName: "",
            importedAt: ""
          } satisfies ExploreImportedPluginState];
        }

        return [key, value];
      })
    );
  } catch {
    return {};
  }
}

function tryFormatJson(value?: string): string {
  if (!value) {
    return "{}";
  }

  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

function toModelConfigDraft(item?: ModelConfigItem): ModelConfigDraft {
  if (!item) {
    return { ...DEFAULT_MODEL_CONFIG_DRAFT };
  }

  return {
    name: item.name,
    providerType: item.providerType,
    apiKey: "",
    baseUrl: item.baseUrl || "",
    defaultModel: item.defaultModel,
    modelId: item.modelId || "",
    systemPrompt: item.systemPrompt || "",
    isEnabled: item.isEnabled,
    supportsEmbedding: item.supportsEmbedding,
    enableStreaming: item.enableStreaming ?? true,
    enableReasoning: item.enableReasoning ?? false,
    enableTools: item.enableTools ?? false,
    enableVision: item.enableVision ?? false,
    enableJsonMode: item.enableJsonMode ?? false,
    temperature: item.temperature,
    maxTokens: item.maxTokens,
    topP: item.topP,
    frequencyPenalty: item.frequencyPenalty,
    presencePenalty: item.presencePenalty
  };
}

function toCreateModelConfigRequest(draft: ModelConfigDraft): ModelConfigCreateRequest {
  return {
    name: draft.name.trim(),
    providerType: draft.providerType.trim(),
    apiKey: draft.apiKey.trim(),
    baseUrl: draft.baseUrl.trim(),
    defaultModel: draft.defaultModel.trim(),
    supportsEmbedding: draft.supportsEmbedding,
    modelId: draft.modelId.trim() || undefined,
    systemPrompt: draft.systemPrompt.trim() || undefined,
    enableStreaming: draft.enableStreaming,
    enableReasoning: draft.enableReasoning,
    enableTools: draft.enableTools,
    enableVision: draft.enableVision,
    enableJsonMode: draft.enableJsonMode,
    temperature: draft.temperature,
    maxTokens: draft.maxTokens,
    topP: draft.topP,
    frequencyPenalty: draft.frequencyPenalty,
    presencePenalty: draft.presencePenalty
  };
}

function toUpdateModelConfigRequest(draft: ModelConfigDraft): ModelConfigUpdateRequest {
  return {
    name: draft.name.trim(),
    apiKey: draft.apiKey.trim(),
    baseUrl: draft.baseUrl.trim(),
    defaultModel: draft.defaultModel.trim(),
    isEnabled: draft.isEnabled,
    supportsEmbedding: draft.supportsEmbedding,
    modelId: draft.modelId.trim() || undefined,
    systemPrompt: draft.systemPrompt.trim() || undefined,
    enableStreaming: draft.enableStreaming,
    enableReasoning: draft.enableReasoning,
    enableTools: draft.enableTools,
    enableVision: draft.enableVision,
    enableJsonMode: draft.enableJsonMode,
    temperature: draft.temperature,
    maxTokens: draft.maxTokens,
    topP: draft.topP,
    frequencyPenalty: draft.frequencyPenalty,
    presencePenalty: draft.presencePenalty
  };
}

function CardGrid<T>({
  testId,
  items,
  render
}: {
  testId: string;
  items: T[];
  render: (item: T) => ReactNode;
}) {
  if (items.length === 0) {
    return <div data-testid={testId}><Empty title="No data" image={null} /></div>;
  }

  return <div className="module-studio__grid" data-testid={testId}>{items.map(render)}</div>;
}

interface SecurityIncidentTaskCard {
  title: string;
  summary: string;
  severity: string;
  ownerSuggestion: string;
  nextActions: string[];
}

type JsonPrimitive = string | number | boolean | null;
type JsonValue = JsonPrimitive | JsonValue[] | { [key: string]: JsonValue };
type JsonObject = { [key: string]: JsonValue };

function isJsonObject(value: JsonValue | undefined): value is JsonObject {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function parseJsonSafely(value?: string): JsonValue | undefined {
  if (!value) {
    return undefined;
  }

  try {
    return JSON.parse(value) as JsonValue;
  } catch {
    const decoded = decodeHtmlEntities(value);
    if (!decoded || decoded === value) {
      return undefined;
    }

    try {
      return JSON.parse(decoded) as JsonValue;
    } catch {
      return undefined;
    }
  }
}

function decodeHtmlEntities(value: string): string {
  return value
    .replaceAll("&quot;", "\"")
    .replaceAll("&#34;", "\"")
    .replaceAll("&#x27;", "'")
    .replaceAll("&lt;", "<")
    .replaceAll("&gt;", ">")
    .replaceAll("&amp;", "&");
}

function toSecurityIncidentTaskCard(outputsJson?: string): SecurityIncidentTaskCard | null {
  const initial = parseJsonSafely(outputsJson);
  if (typeof initial === "string") {
    const nested = parseJsonSafely(initial);
    return isJsonObject(nested) ? extractSecurityIncidentTaskCard(nested) : null;
  }

  if (!isJsonObject(initial)) {
    return null;
  }

  if (typeof initial.result === "string") {
    const nested = parseJsonSafely(initial.result);
    if (isJsonObject(nested)) {
      return extractSecurityIncidentTaskCard(nested);
    }
  }

  return extractSecurityIncidentTaskCard(initial);
}

function extractSecurityIncidentTaskCard(payload: JsonObject): SecurityIncidentTaskCard | null {
  const nextActions = Array.isArray(payload.nextActions)
    ? payload.nextActions.filter((item): item is string => typeof item === "string" && item.trim().length > 0)
    : [];
  const title = typeof payload.title === "string" ? payload.title.trim() : "";
  const summary = typeof payload.summary === "string" ? payload.summary.trim() : "";
  const severity = typeof payload.severity === "string" ? payload.severity.trim() : "";
  const ownerSuggestion = typeof payload.ownerSuggestion === "string" ? payload.ownerSuggestion.trim() : "";

  if (!title && !summary && !severity && !ownerSuggestion && nextActions.length === 0) {
    return null;
  }

  return {
    title: title || "安全事件处置任务",
    summary: summary || "工作流已完成结构化输出。",
    severity: severity || "待确认",
    ownerSuggestion: ownerSuggestion || "安全管理员",
    nextActions
  };
}

function formatWorkflowResultMessage(card: SecurityIncidentTaskCard): string {
  const lines = [
    `任务标题：${card.title}`,
    `事件摘要：${card.summary}`,
    `严重级别：${card.severity}`,
    `建议负责人：${card.ownerSuggestion}`
  ];

  if (card.nextActions.length > 0) {
    lines.push("下一步动作：");
    card.nextActions.forEach((item, index) => {
      lines.push(`${index + 1}. ${item}`);
    });
  }

  return lines.join("\n");
}

function formatDate(value?: string, locale: StudioLocale = "zh-CN") {
  if (!value) {
    return "-";
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString(locale === "en-US" ? "en-US" : "zh-CN");
}

function formatModelConfigEndpointSummary(baseUrl: string | undefined, locale: StudioLocale): string {
  return baseUrl?.trim()
    ? localizedText(locale, "接入地址已配置", "Endpoint configured")
    : localizedText(locale, "接入地址未配置", "Endpoint not configured");
}

export function DevelopPage({
  api,
  locale,
  focus = "overview",
  workflowItems,
  chatflowItems,
  onOpenBot,
  onOpenUsers,
  onOpenRoles,
  onOpenDepartments,
  onOpenPositions,
  onOpenWorkflow,
  onOpenChatflow,
  onOpenWorkflows,
  onOpenChatflows,
  onOpenAgentChat,
  onOpenModelConfigs,
  onOpenLibrary,
  onOpenApplicationDetail,
  onOpenApplicationPublish,
  onCreateWorkflow,
  onCreateChatflow
}: StudioPageProps & {
  focus?: DevelopFocus;
  workflowItems: DevelopResourceSummary[];
  chatflowItems: DevelopResourceSummary[];
  onOpenBot: (botId: string) => void;
  onOpenUsers: () => void;
  onOpenRoles: () => void;
  onOpenDepartments: () => void;
  onOpenPositions: () => void;
  onOpenWorkflow: (workflowId: string) => void;
  onOpenChatflow: (workflowId: string) => void;
  onOpenWorkflows: () => void;
  onOpenChatflows: () => void;
  onOpenAgentChat: () => void;
  onOpenModelConfigs: () => void;
  onOpenLibrary: () => void;
  onOpenApplicationDetail: (appId: string) => void;
  onOpenApplicationPublish: (appId: string) => void;
  onCreateWorkflow: () => void;
  onCreateChatflow: () => void;
}) {
  const [items, setItems] = useState<AgentListItem[]>([]);
  const [models, setModels] = useState<ModelConfigItem[]>([]);
  const [workspaceOverview, setWorkspaceOverview] = useState<StudioWorkspaceOverview | null>(null);
  const [workspaceSummary, setWorkspaceSummary] = useState<WorkspaceIdeSummary | null>(null);
  const [workspaceResources, setWorkspaceResources] = useState<WorkspaceIdeResource[]>([]);
  const [activeFocus, setActiveFocus] = useState<DevelopFocus>(focus);
  const [keyword, setKeyword] = useState("");
  const deferredKeyword = useDeferredValue(keyword);
  const [favoriteOnly, setFavoriteOnly] = useState(false);
  const [agentDialogVisible, setAgentDialogVisible] = useState(false);
  const [applicationDialogVisible, setApplicationDialogVisible] = useState(false);
  const [applicationEditing, setApplicationEditing] = useState<StudioApplicationSummary | null>(null);
  const [selectedApplicationId, setSelectedApplicationId] = useState("");
  const [selectedApplicationDetail, setSelectedApplicationDetail] = useState<StudioApplicationSummary | null>(null);
  const [selectedApplicationPublishRecords, setSelectedApplicationPublishRecords] = useState<StudioApplicationPublishRecord[]>([]);
  const [selectedApplicationConversationTemplates, setSelectedApplicationConversationTemplates] = useState<StudioApplicationConversationTemplate[]>([]);
  const [applicationDetailLoading, setApplicationDetailLoading] = useState(false);
  const [agentDraft, setAgentDraft] = useState({ name: "", description: "" });
  const [applicationDraft, setApplicationDraft] = useState<StudioApplicationCreateRequest>({
    name: "",
    description: "",
    icon: ""
  });
  const [applicationTemplateDraft, setApplicationTemplateDraft] = useState<StudioApplicationConversationTemplateCreateRequest>({
    name: "",
    createMethod: "manual",
    isDefault: false
  });
  const [submitting, setSubmitting] = useState(false);
  const L = (zhCN: string, enUS: string) => localizedText(locale, zhCN, enUS);
  const localizeErrorMessage = (error: Error | null, zhCN: string, enUS: string) => {
    const fallback = L(zhCN, enUS);
    if (!error) {
      return fallback;
    }

    const message = error.message.trim();
    if (!message || /^Request failed with status code \d+$/i.test(message) || /^Network Error$/i.test(message)) {
      return fallback;
    }

    return `${fallback} ${message}`;
  };

  const load = async () => {
    try {
      const [agentsResult, modelResult, workspaceResult, workspaceSummaryResult, workspaceResourcesResult] = await Promise.all([
        api.listAgents({ pageIndex: 1, pageSize: 20, keyword: deferredKeyword || undefined }),
        api.listModelConfigs(),
        api.getWorkspaceOverview(),
        api.getWorkspaceSummary(),
        api.listWorkspaceResources({
          keyword: deferredKeyword || undefined,
          favoriteOnly,
          pageIndex: 1,
          pageSize: 120
        })
      ]);
      setItems(agentsResult.items);
      setModels(modelResult.items);
      setWorkspaceOverview(workspaceResult);
      setWorkspaceSummary(workspaceSummaryResult);
      setWorkspaceResources(workspaceResourcesResult.items);
      const applicationResources = workspaceResourcesResult.items.filter(item => item.resourceType === "app");
      setSelectedApplicationId(current => current || applicationResources[0]?.resourceId || "");
    } catch (error) {
      setWorkspaceOverview(current => current ?? {
        appId: "",
        memberCount: 0,
        roleCount: 0,
        departmentCount: 0,
        positionCount: 0,
        projectCount: 0,
        uncoveredMemberCount: 0,
        applications: []
      });
      setWorkspaceSummary(current => current ?? {
        appCount: 0,
        agentCount: 0,
        workflowCount: 0,
        chatflowCount: 0,
        pluginCount: 0,
        knowledgeBaseCount: 0,
        databaseCount: 0,
        favoriteCount: 0,
        recentCount: 0
      });
      setWorkspaceResources([]);
      Toast.error(localizeErrorMessage(error instanceof Error ? error : null, "加载开发台数据失败。", "Failed to load the studio workspace."));
    }
  };

  useEffect(() => {
    void load();
  }, [api, deferredKeyword, favoriteOnly]);

  useEffect(() => {
    setActiveFocus(focus);
  }, [focus]);

  const applications = useMemo(
    () =>
      workspaceResources
        .filter(item => item.resourceType === "app")
        .map(item => ({
          id: item.resourceId,
          name: item.name,
          description: item.description,
          icon: item.icon,
          status: item.status,
          publishVersion: item.badge?.startsWith("v") ? Number(item.badge.slice(1)) || undefined : undefined,
          workflowId: item.linkedWorkflowId,
          entryRoute: item.entryRoute,
          isFavorite: item.isFavorite,
          updatedAt: item.updatedAt,
          lastEditedAt: item.lastEditedAt,
          badge: item.badge
        })),
    [workspaceResources]
  );
  const selectedApplication = useMemo(
    () => applications.find(item => item.id === selectedApplicationId) ?? applications[0] ?? null,
    [applications, selectedApplicationId]
  );
  const selectedApplicationView = selectedApplicationDetail ?? selectedApplication;

  useEffect(() => {
    let cancelled = false;

    async function loadSelectedApplicationDetail(appId: string) {
      setApplicationDetailLoading(true);
      try {
        const [detail, publishRecords, templates] = await Promise.all([
          api.getApplication(appId),
          api.getApplicationPublishRecords(appId),
          api.getApplicationConversationTemplates(appId)
        ]);

        if (cancelled) {
          return;
        }

        setSelectedApplicationDetail(detail);
        setSelectedApplicationPublishRecords(publishRecords);
        setSelectedApplicationConversationTemplates(templates);
      } catch (error) {
        if (!cancelled) {
          setSelectedApplicationDetail(null);
          setSelectedApplicationPublishRecords([]);
          setSelectedApplicationConversationTemplates([]);
          Toast.error(localizeErrorMessage(error instanceof Error ? error : null, "加载应用详情失败。", "Failed to load the application details."));
        }
      } finally {
        if (!cancelled) {
          setApplicationDetailLoading(false);
        }
      }
    }

    if (selectedApplication?.id) {
      void loadSelectedApplicationDetail(selectedApplication.id);
    } else {
      setSelectedApplicationDetail(null);
      setSelectedApplicationPublishRecords([]);
      setSelectedApplicationConversationTemplates([]);
      setApplicationDetailLoading(false);
    }

    return () => {
      cancelled = true;
    };
  }, [api, selectedApplication?.id]);

  const recentResources = useMemo(() => {
    return [...workspaceResources]
      .sort((left, right) => String(right.lastEditedAt || right.updatedAt || "").localeCompare(String(left.lastEditedAt || left.updatedAt || "")))
      .slice(0, 8);
  }, [workspaceResources]);

  const summaryCards = [
    {
      key: "projects",
      title: L("应用", "Applications"),
      description: L("把项目资源、组织协作和编排能力收拢到同一个应用工作台里。", "Bring app resources, collaboration, and orchestration into one shared delivery surface."),
      count: workspaceSummary?.appCount ?? applications.length,
      actionLabel: L("查看应用", "View Apps"),
      onClick: () => setActiveFocus("projects")
    },
    {
      key: "agents",
      title: "Agent",
      description: L("配置智能体、提示词和对话调试。", "Configure agents, prompt behavior, and conversation debugging."),
      count: workspaceSummary?.agentCount ?? items.length,
      actionLabel: L("进入 Agent", "Open Agent"),
      onClick: () => setActiveFocus("agents")
    },
    {
      key: "workflow",
      title: "Workflow",
      description: L("编排自动化流程与测试运行。", "Design automation flows and validate runs."),
      count: workspaceSummary?.workflowCount ?? workflowItems.length,
      actionLabel: L("进入 Workflow", "Open Workflow"),
      onClick: onOpenWorkflows
    },
    {
      key: "chatflow",
      title: "Chatflow",
      description: L("面向对话流的编排与发布。", "Build and ship orchestration for conversational flows."),
      count: workspaceSummary?.chatflowCount ?? chatflowItems.length,
      actionLabel: L("进入 Chatflow", "Open Chatflow"),
      onClick: onOpenChatflows
    },
    {
      key: "models",
      title: L("模型配置", "Model Configs"),
      description: L("管理应用端大模型供应商与默认模型。", "Manage model providers and default models for runtime."),
      count: models.length,
      actionLabel: L("打开模型配置", "Open Models"),
      onClick: onOpenModelConfigs
    },
    {
      key: "members",
      title: L("组织成员", "Members"),
      description: L("组织架构能力并入 Coze 菜单，开发和治理不再割裂。", "Governance lives alongside creation so delivery and organization stay aligned."),
      count: workspaceOverview?.memberCount ?? 0,
      actionLabel: L("成员管理", "Manage Members"),
      onClick: onOpenUsers
    }
  ];

  const filteredAgents = useMemo(
    () => items.filter((item) => !deferredKeyword || item.name.toLowerCase().includes(deferredKeyword.toLowerCase())),
    [deferredKeyword, items]
  );
  const filteredApplications = useMemo(() => applications, [applications]);
  const pluginResources = useMemo(
    () => workspaceResources.filter(item => item.resourceType === "plugin"),
    [workspaceResources]
  );
  const dataResources = useMemo(
    () => workspaceResources.filter(item => item.resourceType === "knowledge-base" || item.resourceType === "database"),
    [workspaceResources]
  );
  const filteredWorkflows = useMemo(
    () => workflowItems.filter((item) => !deferredKeyword || item.title.toLowerCase().includes(deferredKeyword.toLowerCase())),
    [deferredKeyword, workflowItems]
  );
  const filteredChatflows = useMemo(
    () => chatflowItems.filter((item) => !deferredKeyword || item.title.toLowerCase().includes(deferredKeyword.toLowerCase())),
    [chatflowItems, deferredKeyword]
  );

  function openCreateAgentDialog() {
    setAgentDraft({ name: "", description: "" });
    setAgentDialogVisible(true);
  }

  function openCreateApplicationDialog() {
    setApplicationEditing(null);
    setApplicationDraft({
      name: "",
      description: "",
      icon: ""
    });
    setApplicationDialogVisible(true);
  }

  function openEditApplicationDialog(item: StudioApplicationSummary) {
    setApplicationEditing(item);
    setApplicationDraft({
      name: item.name,
      description: item.description ?? "",
      icon: item.icon ?? ""
    });
    setApplicationDialogVisible(true);
  }

  async function handleCreateAgent() {
    if (!agentDraft.name.trim()) {
      Toast.warning(L("请先填写智能体名称。", "Enter an agent name first."));
      return;
    }

    setSubmitting(true);
    try {
      const botId = await api.createAgent({
        name: agentDraft.name.trim(),
        description: agentDraft.description.trim() || undefined,
        systemPrompt: composeAgentPromptSections({
          ...EMPTY_AGENT_PROMPT_SECTIONS,
          persona: agentDraft.description.trim() || L(`${agentDraft.name.trim()} 的角色说明`, `Role profile for ${agentDraft.name.trim()}`)
        }),
        enableMemory: true,
        enableShortTermMemory: true,
        enableLongTermMemory: true,
        longTermMemoryTopK: 3
      });
      setAgentDialogVisible(false);
      Toast.success(L("智能体已创建。", "Agent created."));
      await load();
      onOpenBot(botId);
    } catch (error) {
      Toast.error(localizeErrorMessage(error instanceof Error ? error : null, "创建智能体失败。", "Failed to create the agent."));
    } finally {
      setSubmitting(false);
    }
  }

  async function handleSaveApplication() {
    if (!applicationDraft.name.trim()) {
      Toast.warning(L("请先填写应用名称。", "Enter an application name first."));
      return;
    }

    setSubmitting(true);
    try {
      if (applicationEditing) {
        await api.updateApplication(applicationEditing.id, {
          name: applicationDraft.name.trim(),
          description: applicationDraft.description?.trim() || undefined,
          icon: applicationDraft.icon?.trim() || undefined
        });
        Toast.success(L("应用信息已更新。", "Application updated."));
      } else {
        const created = await api.createApplication({
          name: applicationDraft.name.trim(),
          description: applicationDraft.description?.trim() || undefined,
          icon: applicationDraft.icon?.trim() || undefined
        });
        Toast.success(L("应用已创建。", "Application created."));
        onOpenWorkflow(created.workflowId);
      }
      setApplicationDialogVisible(false);
      await load();
    } catch (error) {
      Toast.error(localizeErrorMessage(error instanceof Error ? error : null, "保存应用失败。", "Failed to save the application."));
    } finally {
      setSubmitting(false);
    }
  }

  async function handleDeleteApplication(item: StudioApplicationSummary) {
    Modal.confirm({
      title: L("删除应用", "Delete Application"),
      content: L(`确定删除应用 ${item.name} 吗？`, `Delete application ${item.name}?`),
      onOk: async () => {
        try {
          await api.deleteApplication(item.id);
          Toast.success(L("应用已删除。", "Application deleted."));
          await load();
        } catch (error) {
          Toast.error(localizeErrorMessage(error instanceof Error ? error : null, "删除应用失败。", "Failed to delete the application."));
        }
      }
    });
  }

  async function handlePublishApplication(item: StudioApplicationSummary) {
    const releaseNote = window.prompt(L("请输入发布说明（可选）", "Enter release notes (optional)"), "") ?? "";
    try {
      await api.publishApplication(item.id, releaseNote.trim() || undefined);
      const [detail, publishRecords, templates] = await Promise.all([
        api.getApplication(item.id),
        api.getApplicationPublishRecords(item.id),
        api.getApplicationConversationTemplates(item.id)
      ]);
      setSelectedApplicationDetail(detail);
      setSelectedApplicationPublishRecords(publishRecords);
      setSelectedApplicationConversationTemplates(templates);
      Toast.success(L("应用已发布。", "Application published."));
      await load();
      setSelectedApplicationId(item.id);
    } catch (error) {
      Toast.error(localizeErrorMessage(error instanceof Error ? error : null, "发布应用失败。", "Failed to publish the application."));
    }
  }

  async function handleCreateApplicationConversationTemplate() {
    if (!selectedApplication?.id) {
      Toast.warning(L("请先选择一个应用。", "Select an application first."));
      return;
    }

    if (!applicationTemplateDraft.name.trim()) {
      Toast.warning(L("请先填写会话模板名称。", "Enter a conversation template name first."));
      return;
    }

    try {
      await api.createApplicationConversationTemplate(selectedApplication.id, {
        ...applicationTemplateDraft,
        name: applicationTemplateDraft.name.trim()
      });
      Toast.success(L("会话模板已创建。", "Conversation template created."));
      setApplicationTemplateDraft({
        name: "",
        createMethod: "manual",
        isDefault: false
      });
      const templates = await api.getApplicationConversationTemplates(selectedApplication.id);
      setSelectedApplicationConversationTemplates(templates);
    } catch (error) {
      Toast.error(localizeErrorMessage(error instanceof Error ? error : null, "创建会话模板失败。", "Failed to create the conversation template."));
    }
  }

  async function handleDeleteApplicationConversationTemplate(templateId: string) {
    if (!selectedApplication?.id) {
      return;
    }

    try {
      await api.deleteApplicationConversationTemplate(selectedApplication.id, templateId);
      Toast.success(L("会话模板已删除。", "Conversation template deleted."));
      const templates = await api.getApplicationConversationTemplates(selectedApplication.id);
      setSelectedApplicationConversationTemplates(templates);
    } catch (error) {
      Toast.error(localizeErrorMessage(error instanceof Error ? error : null, "删除会话模板失败。", "Failed to delete the conversation template."));
    }
  }

  async function handleToggleFavorite(resource: WorkspaceIdeResource) {
    try {
      await api.toggleWorkspaceFavorite(resource.resourceType, resource.resourceId, !resource.isFavorite);
      await load();
    } catch (error) {
      Toast.error(localizeErrorMessage(error instanceof Error ? error : null, "更新收藏状态失败。", "Failed to update the favorite state."));
    }
  }

  async function handleRecordActivity(resource: WorkspaceIdeResource) {
    try {
      await api.recordWorkspaceActivity({
        resourceType: resource.resourceType,
        resourceId: Number(resource.resourceId),
        resourceTitle: resource.name,
        entryRoute: resource.entryRoute
      });
    } catch {
      // 最近编辑记录失败不阻断主交互。
    }
  }

  const formatDevelopDate = (value?: string) => formatDate(value, locale);
  const getRecentResourceLabel = (resourceType: WorkspaceIdeResource["resourceType"]) => {
    switch (resourceType) {
      case "app":
        return L("应用", "App");
      case "agent":
        return L("智能体", "Agent");
      case "workflow":
        return "Workflow";
      case "chatflow":
        return "Chatflow";
      case "knowledge-base":
        return L("知识库", "Knowledge Base");
      case "database":
        return L("数据库", "Database");
      case "plugin":
        return "Plugin";
      default:
        return resourceType;
    }
  };
  const getRecentResourceActionLabel = (resourceType: WorkspaceIdeResource["resourceType"]) => {
    switch (resourceType) {
      case "plugin":
      case "knowledge-base":
      case "database":
        return L("打开资源库", "Open Library");
      default:
        return L("继续编辑", "Continue");
    }
  };

  return (
    <Surface
      title={L("项目开发", "Develop")}
      subtitle={L("按 Coze 风格把应用、智能体、工作流与组织协作放进同一套开发台。", "Bring apps, agents, workflows, and team collaboration into one Coze-style workspace.")}
      testId="app-develop-page"
      toolbar={
        <Dropdown
          position="bottomRight"
          render={
            <div className="module-studio__coze-menu">
              <button type="button" className="module-studio__coze-menu-item" onClick={openCreateApplicationDialog}>{L("新建应用", "New App")}</button>
              <button type="button" className="module-studio__coze-menu-item" onClick={openCreateAgentDialog}>{L("新建智能体", "New Agent")}</button>
              <button type="button" className="module-studio__coze-menu-item" onClick={onCreateWorkflow}>{L("新建工作流", "New Workflow")}</button>
              <button type="button" className="module-studio__coze-menu-item" onClick={onCreateChatflow}>{L("新建对话流", "New Chatflow")}</button>
            </div>
          }
        >
          <Button icon={<IconPlus />} theme="solid" type="primary" data-testid="app-develop-create-menu">
            {L("创建", "Create")}
          </Button>
        </Dropdown>
      }
    >
      <div className="module-studio__coze-page">
        <section className="module-studio__coze-hero">
          <div className="module-studio__coze-hero-copy">
            <span className="module-studio__coze-kicker">Workspace IDE</span>
            <Typography.Title heading={2} style={{ margin: 0 }}>{L("用 Coze 风格工作空间组织应用、智能体和团队协作", "Organize apps, agents, and team collaboration in a Coze-style workspace")}</Typography.Title>
            <Typography.Text type="tertiary">
              {L("从这里直接进入应用卡片、智能体编排、工作流资源、模型配置和组织治理，不需要在多个后台之间来回切换。", "Jump straight into apps, agent orchestration, workflow assets, model settings, and workspace governance without bouncing between back offices.")}
            </Typography.Text>
          </div>
          <div className="module-studio__coze-hero-actions">
            <Button theme="solid" type="primary" onClick={onOpenAgentChat}>{L("预览与调试", "Preview & Debug")}</Button>
            <Button onClick={onOpenLibrary}>{L("资源库", "Library")}</Button>
            <Button onClick={onOpenModelConfigs}>{L("模型配置", "Models")}</Button>
          </div>
        </section>

        <CardGrid
          testId="app-develop-summary-grid"
          items={summaryCards}
          render={item => (
            <article key={item.key} className="module-studio__coze-metric">
              <span className="module-studio__coze-metric-label">{item.title}</span>
              <strong>{item.count}</strong>
              <p>{item.description}</p>
              <Button onClick={item.onClick}>{item.actionLabel}</Button>
            </article>
          )}
        />

        <div className="module-studio__coze-toolbar">
          <Input
            value={keyword}
            onChange={setKeyword}
            placeholder={L("搜索应用、智能体、工作流、对话流", "Search apps, agents, workflows, and chatflows")}
            showClear
            data-testid="app-develop-search"
          />
          <Space wrap>
            <Radio.Group
              type="button"
              value={activeFocus}
              onChange={event => {
                const nextFocus = event.target.value as DevelopFocus;
                startTransition(() => setActiveFocus(nextFocus));
              }}
            >
              <Radio value="overview">{L("总览", "Overview")}</Radio>
              <Radio value="projects">{L("应用", "Apps")}</Radio>
              <Radio value="agents">Agent</Radio>
              <Radio value="workflow">Workflow</Radio>
              <Radio value="chatflow">Chatflow</Radio>
              <Radio value="plugins">{L("插件", "Plugins")}</Radio>
              <Radio value="data">{L("数据", "Data")}</Radio>
              <Radio value="models">{L("模型", "Models")}</Radio>
              <Radio value="chat">{L("最近编辑", "Recent")}</Radio>
            </Radio.Group>
            <Switch
              checked={favoriteOnly}
              onChange={checked => setFavoriteOnly(checked)}
              checkedText={L("仅收藏", "Favorites")}
              uncheckedText={L("全部资源", "All Resources")}
            />
          </Space>
        </div>

        <div className="module-studio__coze-board">
          <div className="module-studio__coze-board-main">
            {(activeFocus === "overview" || activeFocus === "projects") ? (
              <section className="module-studio__coze-section">
                <div className="module-studio__section-head">
                  <div>
                    <Typography.Title heading={5} style={{ margin: 0 }}>{L("应用", "Apps")}</Typography.Title>
                    <Typography.Text type="tertiary">{L("应用卡片承载项目级组织、资源入口和业务编排上下文。", "App cards carry project structure, resource entry points, and orchestration context.")}</Typography.Text>
                  </div>
                  <Space>
                    <Button onClick={openCreateApplicationDialog}>{L("新建应用", "New App")}</Button>
                    <Button theme="borderless" onClick={() => setActiveFocus("projects")}>{L("查看全部", "View All")}</Button>
                  </Space>
                </div>
                <CardGrid
                  testId="app-develop-projects-grid"
                  items={filteredApplications}
                  render={(item: StudioApplicationSummary) => (
                    <article key={item.id} className={`module-studio__coze-card${selectedApplication?.id === item.id ? " is-active" : ""}`}>
                      <div className="module-studio__card-head">
                        <div>
                          <Tag color="light-blue">{L("应用", "App")}</Tag>
                          <strong>{item.name}</strong>
                        </div>
                        <Space>
                          {item.status ? <Tag color={item.status.toLowerCase() === "published" ? "green" : "blue"}>{item.status}</Tag> : null}
                          <Button theme="borderless" onClick={() => void handleToggleFavorite({
                            resourceType: "app",
                            resourceId: item.id,
                            name: item.name,
                            description: item.description,
                            icon: item.icon,
                            status: item.status || "draft",
                            publishStatus: item.status?.toLowerCase() === "published" ? "published" : "draft",
                            updatedAt: item.updatedAt || "",
                            isFavorite: Boolean(item.isFavorite),
                            entryRoute: item.entryRoute || "",
                            badge: item.badge,
                            linkedWorkflowId: item.workflowId
                          })}>
                            {item.isFavorite ? "★" : "☆"}
                          </Button>
                        </Space>
                      </div>
                      <p>{item.description || L("AI 应用将智能体、工作流和提示模板组织成统一交付入口。", "AI apps bundle agents, workflows, and prompt templates into a single delivery entry.")}</p>
                      <span className="module-studio__meta">{L("最近更新：", "Updated: ")}{formatDevelopDate(item.lastEditedAt || item.updatedAt)}</span>
                      <div className="module-studio__actions">
                        <Button onClick={() => setSelectedApplicationId(item.id)}>{L("侧边栏详情", "Sidebar Details")}</Button>
                        <Button theme="light" onClick={() => onOpenApplicationDetail(item.id)}>{L("详情页", "Details")}</Button>
                        <Button
                          theme="solid"
                          type="primary"
                          onClick={() => {
                            void handleRecordActivity({
                              resourceType: "app",
                              resourceId: item.id,
                              name: item.name,
                              description: item.description,
                              icon: item.icon,
                              status: item.status || "draft",
                              publishStatus: item.status?.toLowerCase() === "published" ? "published" : "draft",
                              updatedAt: item.updatedAt || "",
                              isFavorite: Boolean(item.isFavorite),
                              entryRoute: item.entryRoute || "",
                              badge: item.badge,
                              linkedWorkflowId: item.workflowId
                            }).then(() => {
                              if (item.workflowId) {
                                onOpenWorkflow(item.workflowId);
                              }
                            });
                          }}
                        >
                          {L("打开 IDE", "Open IDE")}
                        </Button>
                        <Button theme="borderless" onClick={() => openEditApplicationDialog(item)}>{L("编辑", "Edit")}</Button>
                        <Button theme="borderless" type="danger" onClick={() => void handleDeleteApplication(item)}>{L("删除", "Delete")}</Button>
                      </div>
                    </article>
                  )}
                />
              </section>
            ) : null}

            {(activeFocus === "overview" || activeFocus === "agents") ? (
              <section className="module-studio__coze-section">
                <div className="module-studio__section-head">
                  <div>
                    <Typography.Title heading={5} style={{ margin: 0 }}>{L("智能体", "Agents")}</Typography.Title>
                    <Typography.Text type="tertiary">{L("把模型、记忆和工作流绑定在智能体上，进入专属 IDE 做调试和发布。", "Bind models, memory, and workflows to agents, then debug and publish in a dedicated IDE.")}</Typography.Text>
                  </div>
                  <Button theme="borderless" onClick={() => setActiveFocus("agents")}>{L("查看全部", "View All")}</Button>
                </div>
                <CardGrid
                  testId="app-develop-agents-grid"
                  items={filteredAgents}
                  render={(item: AgentListItem) => (
                    <article key={item.id} className="module-studio__coze-card">
                      <div className="module-studio__card-head">
                        <div>
                          <Tag color="cyan">{L("智能体", "Agent")}</Tag>
                          <strong>{item.name}</strong>
                        </div>
                        <Tag color="blue">{item.status}</Tag>
                      </div>
                      <p>{item.description || item.modelName || L("暂无描述", "No description")}</p>
                      <span className="module-studio__meta">{L("创建时间：", "Created: ")}{formatDevelopDate(item.createdAt)}</span>
                      <Button onClick={() => onOpenBot(item.id)}>{L("打开 IDE", "Open IDE")}</Button>
                    </article>
                  )}
                />
              </section>
            ) : null}

            {(activeFocus === "overview" || activeFocus === "workflow") ? (
              <section className="module-studio__coze-section">
                <div className="module-studio__section-head">
                  <div>
                    <Typography.Title heading={5} style={{ margin: 0 }}>{L("工作流", "Workflows")}</Typography.Title>
                    <Typography.Text type="tertiary">{L("从业务编排、数据库处理到知识库检索，把可执行逻辑沉淀成标准工作流资产。", "Turn orchestration, data processing, and knowledge retrieval into reusable workflow assets.")}</Typography.Text>
                  </div>
                  <Space>
                    <Button onClick={onCreateWorkflow}>{L("新建工作流", "New Workflow")}</Button>
                    <Button theme="borderless" onClick={onOpenWorkflows}>{L("资源列表", "Resource List")}</Button>
                  </Space>
                </div>
                <CardGrid
                  testId="app-develop-workflows-grid"
                  items={filteredWorkflows}
                  render={(item: DevelopResourceSummary) => (
                    <article key={item.id} className="module-studio__coze-card">
                      <div className="module-studio__card-head">
                        <div>
                          <Tag color="blue">Workflow</Tag>
                          <strong>{item.title}</strong>
                        </div>
                        {item.status ? <Tag color="green">{item.status}</Tag> : null}
                      </div>
                      <p>{item.description || item.meta || L("暂无描述", "No description")}</p>
                      <span className="module-studio__meta">{L("最近更新：", "Updated: ")}{formatDevelopDate(item.updatedAt)}</span>
                      <Button onClick={() => onOpenWorkflow(item.id)}>{L("打开编辑器", "Open Editor")}</Button>
                    </article>
                  )}
                />
              </section>
            ) : null}

            {(activeFocus === "overview" || activeFocus === "chatflow") ? (
              <section className="module-studio__coze-section">
                <div className="module-studio__section-head">
                  <div>
                    <Typography.Title heading={5} style={{ margin: 0 }}>{L("对话流", "Chatflows")}</Typography.Title>
                    <Typography.Text type="tertiary">{L("适合多轮交互、用户引导和 Agent 场景，把会话意图转成流程节点。", "Design multi-turn interactions and guided agent flows by turning conversation intent into flow nodes.")}</Typography.Text>
                  </div>
                  <Space>
                    <Button onClick={onCreateChatflow}>{L("新建对话流", "New Chatflow")}</Button>
                    <Button theme="borderless" onClick={onOpenChatflows}>{L("资源列表", "Resource List")}</Button>
                  </Space>
                </div>
                <CardGrid
                  testId="app-develop-chatflows-grid"
                  items={filteredChatflows}
                  render={(item: DevelopResourceSummary) => (
                    <article key={item.id} className="module-studio__coze-card">
                      <div className="module-studio__card-head">
                        <div>
                          <Tag color="purple">Chatflow</Tag>
                          <strong>{item.title}</strong>
                        </div>
                        {item.status ? <Tag color="purple">{item.status}</Tag> : null}
                      </div>
                      <p>{item.description || item.meta || L("暂无描述", "No description")}</p>
                      <span className="module-studio__meta">{L("最近更新：", "Updated: ")}{formatDevelopDate(item.updatedAt)}</span>
                      <Button onClick={() => onOpenChatflow(item.id)}>{L("打开编辑器", "Open Editor")}</Button>
                    </article>
                  )}
                />
              </section>
            ) : null}

            {(activeFocus === "overview" || activeFocus === "plugins") ? (
              <section className="module-studio__coze-section">
                <div className="module-studio__section-head">
                  <div>
                    <Typography.Title heading={5} style={{ margin: 0 }}>{L("插件", "Plugins")}</Typography.Title>
                    <Typography.Text type="tertiary">{L("把 Bot 技能、HTTP/OpenAPI 能力与工具调用统一沉淀在 Coze 工作空间里。", "Collect bot skills, HTTP/OpenAPI capabilities, and tools in one workspace.")}</Typography.Text>
                  </div>
                  <Button theme="borderless" onClick={onOpenLibrary}>{L("进入资源库", "Open Library")}</Button>
                </div>
                <CardGrid
                  testId="app-develop-plugins-grid"
                  items={pluginResources}
                  render={(item: WorkspaceIdeResource) => (
                    <article key={`${item.resourceType}-${item.resourceId}`} className="module-studio__coze-card">
                      <div className="module-studio__card-head">
                        <div>
                          <Tag color="orange">Plugin</Tag>
                          <strong>{item.name}</strong>
                        </div>
                        <Button theme="borderless" onClick={() => void handleToggleFavorite(item)}>{item.isFavorite ? "★" : "☆"}</Button>
                      </div>
                      <p>{item.description || L("插件工具能力", "Plugin capability")}</p>
                      <span className="module-studio__meta">{L("最近更新：", "Updated: ")}{formatDevelopDate(item.lastEditedAt || item.updatedAt)}</span>
                      <Button onClick={onOpenLibrary}>{L("打开资源库", "Open Library")}</Button>
                    </article>
                  )}
                />
              </section>
            ) : null}

            {(activeFocus === "overview" || activeFocus === "data") ? (
              <section className="module-studio__coze-section">
                <div className="module-studio__section-head">
                  <div>
                    <Typography.Title heading={5} style={{ margin: 0 }}>{L("数据与知识", "Data & Knowledge")}</Typography.Title>
                    <Typography.Text type="tertiary">{L("知识库、数据库和变量继续沉淀在统一工作空间里，为智能体与工作流复用。", "Keep knowledge bases, databases, and variables in one workspace for agents and workflows to reuse.")}</Typography.Text>
                  </div>
                  <Button theme="borderless" onClick={onOpenLibrary}>{L("统一管理", "Manage")}</Button>
                </div>
                <CardGrid
                  testId="app-develop-data-grid"
                  items={dataResources}
                  render={(item: WorkspaceIdeResource) => (
                    <article key={`${item.resourceType}-${item.resourceId}`} className="module-studio__coze-card">
                      <div className="module-studio__card-head">
                        <div>
                          <Tag color={item.resourceType === "knowledge-base" ? "green" : "blue"}>
                            {item.resourceType === "knowledge-base" ? L("知识库", "Knowledge Base") : L("数据库", "Database")}
                          </Tag>
                          <strong>{item.name}</strong>
                        </div>
                        <Button theme="borderless" onClick={() => void handleToggleFavorite(item)}>{item.isFavorite ? "★" : "☆"}</Button>
                      </div>
                      <p>{item.description || L("可被工作流与智能体直接复用的数据资源。", "Data assets that workflows and agents can reuse directly.")}</p>
                      <span className="module-studio__meta">{L("最近更新：", "Updated: ")}{formatDevelopDate(item.lastEditedAt || item.updatedAt)}</span>
                      <Button onClick={onOpenLibrary}>{L("打开资源库", "Open Library")}</Button>
                    </article>
                  )}
                />
              </section>
            ) : null}

            {(activeFocus === "overview" || activeFocus === "models") ? (
              <section className="module-studio__coze-section">
                <div className="module-studio__section-head">
                  <div>
                    <Typography.Title heading={5} style={{ margin: 0 }}>{L("模型配置", "Models")}</Typography.Title>
                    <Typography.Text type="tertiary">{L("统一管理模型供应商、推理能力、工具调用和提示词测试。", "Manage model providers, inference capabilities, tool calling, and prompt testing in one place.")}</Typography.Text>
                  </div>
                  <Button theme="borderless" onClick={onOpenModelConfigs}>{L("管理模型", "Manage Models")}</Button>
                </div>
                <CardGrid
                  testId="app-develop-models-grid"
                  items={models}
                  render={(item: ModelConfigItem) => (
                    <article key={item.id} className="module-studio__coze-card">
                      <div className="module-studio__card-head">
                        <div>
                          <Tag color="green">Model</Tag>
                          <strong>{item.name}</strong>
                        </div>
                        <Tag color={item.isEnabled ? "green" : "grey"}>{item.isEnabled ? L("启用", "Enabled") : L("停用", "Disabled")}</Tag>
                      </div>
                      <p>{item.providerType} / {item.defaultModel}</p>
                      <span className="module-studio__meta">{L("创建时间：", "Created: ")}{formatDevelopDate(item.createdAt)}</span>
                    </article>
                  )}
                />
              </section>
            ) : null}

            {(activeFocus === "overview" || activeFocus === "chat") ? (
              <section className="module-studio__coze-section">
                <div className="module-studio__section-head">
                  <div>
                    <Typography.Title heading={5} style={{ margin: 0 }}>{L("最近编辑", "Recent")}</Typography.Title>
                    <Typography.Text type="tertiary">{L("回到最近访问过的资源，继续编辑、调试或发布。", "Jump back into recently visited resources and keep editing, debugging, or publishing.")}</Typography.Text>
                  </div>
                  <Button theme="borderless" onClick={onOpenAgentChat}>{L("进入对话调试", "Open Chat Debug")}</Button>
                </div>
                <CardGrid
                  testId="app-develop-recent-grid"
                  items={recentResources}
                  render={(item: WorkspaceIdeResource) => (
                    <article key={`${item.resourceType}-${item.resourceId}`} className="module-studio__coze-card module-studio__coze-card--compact">
                      <Tag color={item.resourceType === "chatflow" ? "purple" : item.resourceType === "workflow" ? "blue" : item.resourceType === "agent" ? "cyan" : "green"}>
                        {getRecentResourceLabel(item.resourceType)}
                      </Tag>
                      <strong>{item.name}</strong>
                      <p>{item.description || item.badge || item.status || L("最近访问资源", "Recently visited resource")}</p>
                      <span className="module-studio__meta">{L("最近时间：", "Visited: ")}{formatDevelopDate(item.lastEditedAt || item.updatedAt)}</span>
                      {item.resourceType === "agent" ? <Button onClick={() => onOpenBot(item.resourceId)}>{getRecentResourceActionLabel(item.resourceType)}</Button> : null}
                      {item.resourceType === "workflow" ? <Button onClick={() => onOpenWorkflow(item.resourceId)}>{getRecentResourceActionLabel(item.resourceType)}</Button> : null}
                      {item.resourceType === "chatflow" ? <Button onClick={() => onOpenChatflow(item.resourceId)}>{getRecentResourceActionLabel(item.resourceType)}</Button> : null}
                      {item.resourceType === "app" && item.linkedWorkflowId ? <Button onClick={() => onOpenWorkflow(item.linkedWorkflowId!)}>{getRecentResourceActionLabel(item.resourceType)}</Button> : null}
                      {(item.resourceType === "plugin" || item.resourceType === "knowledge-base" || item.resourceType === "database") ? <Button onClick={onOpenLibrary}>{getRecentResourceActionLabel(item.resourceType)}</Button> : null}
                    </article>
                  )}
                />
              </section>
            ) : null}
          </div>

          <aside className="module-studio__coze-board-side">
            <section className="module-studio__coze-sidepanel">
              <Typography.Title heading={6}>{L("应用详情", "App Details")}</Typography.Title>
              {selectedApplicationView ? (
                <div className="module-studio__stack">
                  <Tag color={selectedApplicationView.status?.toLowerCase() === "published" ? "green" : "blue"}>
                    {selectedApplicationView.status || "Draft"}
                  </Tag>
                  <strong>{selectedApplicationView.name}</strong>
                  <Typography.Text type="tertiary">{selectedApplicationView.description || L("当前应用还没有补充描述。", "This app does not have a description yet.")}</Typography.Text>
                  <span className="module-studio__meta">{L("最近更新：", "Updated: ")}{formatDevelopDate(selectedApplicationView.lastEditedAt || selectedApplicationView.updatedAt)}</span>
                  <Space wrap>
                    <Button
                      theme="solid"
                      type="primary"
                      onClick={() => {
                        if (selectedApplicationView.workflowId) {
                          onOpenWorkflow(selectedApplicationView.workflowId);
                        }
                      }}
                    >
                      {L("进入工作流 IDE", "Open Workflow IDE")}
                    </Button>
                    <Button onClick={() => openEditApplicationDialog(selectedApplicationView)}>{L("编辑应用", "Edit App")}</Button>
                    <Button theme="light" type="tertiary" onClick={() => void handlePublishApplication(selectedApplicationView)}>
                      {L("发布应用", "Publish App")}
                    </Button>
                    <Button theme="borderless" onClick={() => onOpenApplicationPublish(selectedApplicationView.id)}>
                      {L("打开发布页", "Open Publish Page")}
                    </Button>
                  </Space>

                  {applicationDetailLoading ? (
                    <Typography.Text type="tertiary">{L("正在加载应用发布信息…", "Loading publish details...")}</Typography.Text>
                  ) : null}

                  <div className="module-studio__coze-inspector-card">
                    <span>{L("发布记录", "Publish Records")}</span>
                    {selectedApplicationPublishRecords.length === 0 ? (
                      <Typography.Text type="tertiary">{L("当前还没有发布记录。", "No publish records yet.")}</Typography.Text>
                    ) : (
                      <div className="module-studio__stack">
                        {selectedApplicationPublishRecords.slice(0, 3).map(record => (
                          <div key={record.id} className="module-studio__coze-linkrow">
                            <span>{record.version}</span>
                            <strong>{formatDevelopDate(record.createdAt)}</strong>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>

                  <div className="module-studio__coze-inspector-card">
                    <div className="module-studio__card-head">
                      <span>{L("会话模板", "Conversation Templates")}</span>
                      <Tag color="light-blue">{selectedApplicationConversationTemplates.length}</Tag>
                    </div>
                    <div className="module-studio__stack">
                      <Input
                        value={applicationTemplateDraft.name}
                        onChange={value => setApplicationTemplateDraft(current => ({ ...current, name: value }))}
                        placeholder={L("新建会话模板名称", "New template name")}
                      />
                      <Space wrap>
                        <Select
                          value={applicationTemplateDraft.createMethod}
                          optionList={[
                            { label: L("手动创建", "Manual"), value: "manual" },
                            { label: L("节点生成", "Node Generated"), value: "node" }
                          ]}
                          onChange={value =>
                            setApplicationTemplateDraft(current => ({
                              ...current,
                              createMethod: String(value)
                            }))
                          }
                        />
                        <Switch
                          checked={Boolean(applicationTemplateDraft.isDefault)}
                          onChange={checked =>
                            setApplicationTemplateDraft(current => ({
                              ...current,
                              isDefault: checked
                            }))
                          }
                        />
                        <Typography.Text type="tertiary">{L("默认模板", "Default Template")}</Typography.Text>
                      </Space>
                      <Button onClick={() => void handleCreateApplicationConversationTemplate()}>
                        {L("新建模板", "Create Template")}
                      </Button>
                      {selectedApplicationConversationTemplates.length === 0 ? (
                        <Typography.Text type="tertiary">{L("当前还没有会话模板。", "No conversation templates yet.")}</Typography.Text>
                      ) : (
                        selectedApplicationConversationTemplates.slice(0, 4).map(template => (
                          <div key={template.id} className="module-studio__coze-linkrow">
                            <div>
                              <strong>{template.name}</strong>
                              <div className="module-studio__meta">
                                {template.createMethod} / v{template.version}
                                {template.isDefault ? L(" / 默认", " / Default") : ""}
                              </div>
                            </div>
                            <Button
                              theme="borderless"
                              type="danger"
                              onClick={() => void handleDeleteApplicationConversationTemplate(template.id)}
                            >
                              {L("删除", "Delete")}
                            </Button>
                          </div>
                        ))
                      )}
                    </div>
                  </div>
                </div>
              ) : (
                <Empty title={L("暂无应用", "No app selected")} image={null} />
              )}
            </section>

            <section className="module-studio__coze-sidepanel">
              <Typography.Title heading={6}>{L("组织架构", "Organization")}</Typography.Title>
              <div className="module-studio__coze-linklist">
                <button type="button" className="module-studio__coze-linkrow" onClick={onOpenUsers}>
                  <span>{L("成员管理", "Members")}</span>
                  <strong>{workspaceOverview?.memberCount ?? 0}</strong>
                </button>
                <button type="button" className="module-studio__coze-linkrow" onClick={onOpenRoles}>
                  <span>{L("角色管理", "Roles")}</span>
                  <strong>{workspaceOverview?.roleCount ?? 0}</strong>
                </button>
                <button type="button" className="module-studio__coze-linkrow" onClick={onOpenDepartments}>
                  <span>{L("部门管理", "Departments")}</span>
                  <strong>{workspaceOverview?.departmentCount ?? 0}</strong>
                </button>
                <button type="button" className="module-studio__coze-linkrow" onClick={onOpenPositions}>
                  <span>{L("岗位管理", "Positions")}</span>
                  <strong>{workspaceOverview?.positionCount ?? 0}</strong>
                </button>
              </div>
              {(workspaceOverview?.uncoveredMemberCount ?? 0) > 0 ? (
                <Banner
                  type="warning"
                  bordered={false}
                  fullMode={false}
                  title={L("组织治理提醒", "Governance Reminder")}
                  description={L(`当前仍有 ${workspaceOverview?.uncoveredMemberCount ?? 0} 个成员未被角色权限覆盖。`, `${workspaceOverview?.uncoveredMemberCount ?? 0} members are still not covered by role permissions.`)}
                />
              ) : null}
            </section>

            <section className="module-studio__coze-sidepanel">
              <Typography.Title heading={6}>{L("快速入口", "Quick Links")}</Typography.Title>
              <div className="module-studio__coze-linklist">
                <button type="button" className="module-studio__coze-linkrow" onClick={onOpenLibrary}>
                  <span>{L("资源库", "Library")}</span>
                  <strong>Open</strong>
                </button>
                <button type="button" className="module-studio__coze-linkrow" onClick={onOpenAgentChat}>
                  <span>{L("预览与调试", "Preview & Debug")}</span>
                  <strong>Chat</strong>
                </button>
                <button type="button" className="module-studio__coze-linkrow" onClick={onOpenModelConfigs}>
                  <span>{L("模型配置", "Models")}</span>
                  <strong>{models.length}</strong>
                </button>
                <button type="button" className="module-studio__coze-linkrow" onClick={() => setFavoriteOnly(current => !current)}>
                  <span>{L("收藏资源", "Favorites")}</span>
                  <strong>{workspaceSummary?.favoriteCount ?? 0}</strong>
                </button>
                <button type="button" className="module-studio__coze-linkrow" onClick={() => setActiveFocus("chat")}>
                  <span>{L("最近编辑", "Recent")}</span>
                  <strong>{workspaceSummary?.recentCount ?? 0}</strong>
                </button>
              </div>
            </section>
          </aside>
        </div>

        <Modal
          title={L("新建智能体", "New Agent")}
          visible={agentDialogVisible}
          onCancel={() => {
            if (!submitting) {
              setAgentDialogVisible(false);
            }
          }}
          onOk={() => void handleCreateAgent()}
          okText={L("创建", "Create")}
          cancelText={L("取消", "Cancel")}
          confirmLoading={submitting}
        >
          <div className="module-studio__stack">
            <Input value={agentDraft.name} onChange={value => setAgentDraft(current => ({ ...current, name: value }))} placeholder={L("智能体名称", "Agent name")} />
            <Input value={agentDraft.description} onChange={value => setAgentDraft(current => ({ ...current, description: value }))} placeholder={L("角色描述", "Role description")} />
          </div>
        </Modal>

        <Modal
          title={applicationEditing ? L("编辑应用", "Edit App") : L("新建应用", "New App")}
          visible={applicationDialogVisible}
          onCancel={() => {
            if (!submitting) {
              setApplicationDialogVisible(false);
            }
          }}
          onOk={() => void handleSaveApplication()}
          okText={applicationEditing ? L("保存", "Save") : L("创建", "Create")}
          cancelText={L("取消", "Cancel")}
          confirmLoading={submitting}
        >
          <div className="module-studio__stack">
            <Input value={applicationDraft.name} onChange={value => setApplicationDraft(current => ({ ...current, name: value }))} placeholder={L("应用名称", "App name")} />
            <Input
              value={applicationDraft.description ?? ""}
              onChange={value => setApplicationDraft(current => ({ ...current, description: value }))}
              placeholder={L("应用描述", "App description")}
            />
            <Input
              value={applicationDraft.icon ?? ""}
              onChange={value => setApplicationDraft(current => ({ ...current, icon: value }))}
              placeholder={L("图标标识（可选）", "Icon identifier (optional)")}
            />
          </div>
        </Modal>
      </div>
    </Surface>
  );
}

export function AssistantsPage({
  api,
  onOpenDetail,
  onOpenPublish
}: StudioPageProps & {
  onOpenDetail: (assistantId: string) => void;
  onOpenPublish: (assistantId: string) => void;
}) {
  const [items, setItems] = useState<AgentListItem[]>([]);
  const [keyword, setKeyword] = useState("");

  useEffect(() => {
    void api.listAgents({ pageIndex: 1, pageSize: 50, keyword: keyword.trim() || undefined }).then(result => {
      setItems(result.items);
    }).catch(error => {
      Toast.error(error instanceof Error ? error.message : "加载智能体列表失败。");
    });
  }, [api, keyword]);

  return (
    <Surface
      title="智能体中心"
      subtitle="从这里进入智能体详情与发布页面。"
      testId="app-studio-assistants-page"
      toolbar={
        <Input
          value={keyword}
          onChange={setKeyword}
          placeholder="搜索智能体"
          showClear
          data-testid="app-studio-assistants-search"
        />
      }
    >
      <CardGrid
        testId="app-studio-assistants-grid"
        items={items}
        render={(item: AgentListItem) => (
          <article key={item.id} className="module-studio__coze-card">
            <div className="module-studio__card-head">
              <div>
                <Tag color="cyan">Agent</Tag>
                <strong>{item.name}</strong>
              </div>
              <Tag color={item.status?.toLowerCase() === "published" ? "green" : "blue"}>
                {item.status || "Draft"}
              </Tag>
            </div>
            <p>{item.description || "智能体设计、调试与发布。"}</p>
            <span className="module-studio__meta">
              创建时间：{formatDate(item.createdAt)} / 版本：v{item.publishVersion ?? 0}
            </span>
            <Space wrap>
              <Button theme="solid" type="primary" onClick={() => onOpenDetail(item.id)}>
                进入详情
              </Button>
              <Button onClick={() => onOpenPublish(item.id)}>
                进入发布页
              </Button>
            </Space>
          </article>
        )}
      />
    </Surface>
  );
}

export function AppsPage({
  api,
  onOpenDetail,
  onOpenPublish,
  onOpenWorkflow
}: StudioPageProps & {
  onOpenDetail: (appId: string) => void;
  onOpenPublish: (appId: string) => void;
  onOpenWorkflow?: (workflowId: string) => void;
}) {
  const [items, setItems] = useState<WorkspaceIdeResource[]>([]);
  const [keyword, setKeyword] = useState("");

  useEffect(() => {
    void api.listWorkspaceResources({
      resourceType: "app",
      pageIndex: 1,
      pageSize: 100,
      keyword: keyword.trim() || undefined
    }).then(result => {
      setItems(result.items);
    }).catch(error => {
      Toast.error(error instanceof Error ? error.message : "加载应用列表失败。");
    });
  }, [api, keyword]);

  return (
    <Surface
      title="应用中心"
      subtitle="从这里进入应用详情、工作流和发布页面。"
      testId="app-studio-apps-page"
      toolbar={
        <Input
          value={keyword}
          onChange={setKeyword}
          placeholder="搜索应用"
          showClear
          data-testid="app-studio-apps-search"
        />
      }
    >
      <CardGrid
        testId="app-studio-apps-grid"
        items={items}
        render={(item: WorkspaceIdeResource) => (
          <article key={`${item.resourceType}-${item.resourceId}`} className="module-studio__coze-card">
            <div className="module-studio__card-head">
              <div>
                <Tag color="light-blue">应用</Tag>
                <strong>{item.name}</strong>
              </div>
              <Tag color={item.publishStatus?.toLowerCase() === "published" ? "green" : "blue"}>
                {item.publishStatus || item.status || "Draft"}
              </Tag>
            </div>
            <p>{item.description || "应用资源编排与发布。"}</p>
            <span className="module-studio__meta">
              最近更新：{formatDate(item.lastEditedAt || item.updatedAt)} {item.badge ? ` / ${item.badge}` : ""}
            </span>
            <Space wrap>
              <Button theme="solid" type="primary" onClick={() => onOpenDetail(item.resourceId)}>
                进入详情
              </Button>
              <Button onClick={() => onOpenPublish(item.resourceId)}>
                进入发布页
              </Button>
              {item.linkedWorkflowId && onOpenWorkflow ? (
                <Button theme="light" onClick={() => onOpenWorkflow(item.linkedWorkflowId!)}>
                  打开工作流
                </Button>
              ) : null}
            </Space>
          </article>
        )}
      />
    </Surface>
  );
}

export { AgentWorkbench as BotIdePage } from "./assistant/agent-workbench";


export function AgentChatPage({ api }: StudioPageProps) {
  const [agents, setAgents] = useState<AgentListItem[]>([]);
  const [selectedAgentId, setSelectedAgentId] = useState<string>("");
  const [conversations, setConversations] = useState<ConversationItem[]>([]);
  const [messages, setMessages] = useState<ChatMessageItem[]>([]);

  useEffect(() => {
    void api.listAgents({ pageIndex: 1, pageSize: 20 }).then(result => {
      setAgents(result.items);
      const firstAgent = result.items[0];
      if (firstAgent) {
        setSelectedAgentId(firstAgent.id);
      }
    });
  }, [api]);

  useEffect(() => {
    if (!selectedAgentId) {
      return;
    }

    void api.listConversations(selectedAgentId).then(result => {
      setConversations(result.items);
      const firstConversation = result.items[0];
      if (firstConversation) {
        void api.getMessages(firstConversation.id).then(setMessages);
      } else {
        setMessages([]);
      }
    });
  }, [api, selectedAgentId]);

  return (
    <Surface title="Agent Chat" subtitle="应用端对话历史与调试记录。" testId="app-agent-chat-page">
      <div className="module-studio__chat-layout">
        <div className="module-studio__list">
          {agents.map(agent => (
            <Button key={agent.id} theme={agent.id === selectedAgentId ? "solid" : "borderless"} onClick={() => setSelectedAgentId(agent.id)}>
              {agent.name}
            </Button>
          ))}
        </div>
        <div className="module-studio__chat-body">
          <Typography.Title heading={6}>Conversations</Typography.Title>
          <CardGrid
            testId="app-agent-chat-conversations"
            items={conversations}
            render={(item: ConversationItem) => <article key={item.id} className="module-studio__card"><strong>{item.title || item.id}</strong><p>{item.messageCount} messages</p></article>}
          />
          <Typography.Title heading={6}>Messages</Typography.Title>
          <CardGrid
            testId="app-agent-chat-messages"
            items={messages}
            render={(item: ChatMessageItem) => (
              <article key={item.id} className="module-studio__card">
                <strong>{item.role}</strong>
                <pre className="module-studio__message-content">{item.content}</pre>
              </article>
            )}
          />
        </div>
      </div>
    </Surface>
  );
}

export function AiAssistantPage({ api }: StudioPageProps) {
  const [kind, setKind] = useState<"sql" | "workflow">("workflow");
  const [description, setDescription] = useState("");
  const [result, setResult] = useState("");

  return (
    <Surface title="AI Assistant" subtitle="真实调用应用级 AI 辅助生成。" testId="app-ai-assistant-page">
      <div className="module-studio__stack">
        <div className="module-studio__actions">
          <Button theme={kind === "workflow" ? "solid" : "borderless"} onClick={() => setKind("workflow")}>Workflow</Button>
          <Button theme={kind === "sql" ? "solid" : "borderless"} onClick={() => setKind("sql")}>SQL</Button>
        </div>
        <textarea value={description} onChange={event => setDescription(event.target.value)} rows={8} className="module-studio__textarea" />
        <Button onClick={() => void api.generateAssistant(kind, description).then(next => setResult(next?.result || ""))}>Generate</Button>
        <textarea value={result} rows={10} readOnly className="module-studio__textarea" />
      </div>
    </Surface>
  );
}

export function PluginsPage({
  api,
  onOpenLibrary,
  onOpenExplore,
  onOpenDetail
}: StudioPageProps & {
  onOpenLibrary: () => void;
  onOpenExplore: () => void;
  onOpenDetail: (pluginId: number) => void;
}) {
  const [items, setItems] = useState<Array<{ id: number; name: string; category?: string; status: number; sourceType?: number }>>([]);
  const [keyword, setKeyword] = useState("");
  const [importedPlugins, setImportedPlugins] = useState<Record<string, ExploreImportedPluginState>>(() => readExploreImportedPluginStateMap());

  useEffect(() => {
    void api.listPlugins().then(result => {
      const normalizedKeyword = keyword.trim().toLowerCase();
      const filtered = normalizedKeyword
        ? result.filter(item =>
            `${item.name} ${item.category ?? ""}`.toLowerCase().includes(normalizedKeyword)
          )
        : result;
      setItems(filtered);
    }).catch(error => {
      Toast.error(error instanceof Error ? error.message : "加载插件列表失败。");
    });
  }, [api, keyword]);

  useEffect(() => {
    setImportedPlugins(readExploreImportedPluginStateMap());
  }, [items]);

  return (
    <Surface
      title="插件中心"
      subtitle="统一查看当前工作空间插件，并进入资源库或插件市场。"
      testId="app-studio-plugins-page"
      toolbar={
        <Input
          value={keyword}
          onChange={setKeyword}
          placeholder="搜索插件"
          showClear
          data-testid="app-studio-plugins-search"
        />
      }
    >
      <div className="module-studio__stack">
        <Space>
          <Button theme="solid" type="primary" onClick={onOpenLibrary}>进入资源库</Button>
          <Button onClick={onOpenExplore}>打开插件市场</Button>
        </Space>
        <CardGrid
          testId="app-studio-plugins-grid"
          items={items}
          render={(item) => {
            const marketSource = Object.values(importedPlugins).find(source => source.importedPluginId === item.id);
            const classificationTag = resolvePluginClassificationTag(item.sourceType, Boolean(marketSource));
            return (
            <article key={item.id} className="module-studio__coze-card">
              <div className="module-studio__card-head">
                <div>
                  <Tag color="orange">Plugin</Tag>
                  <Tag color={classificationTag.color}>{classificationTag.label}</Tag>
                  <strong>{item.name}</strong>
                </div>
                <Tag color={item.status === 1 ? "green" : "blue"}>{item.status === 1 ? "Published" : "Draft"}</Tag>
              </div>
              <p>{item.category || "工作空间插件能力"}</p>
              {marketSource ? (
                <Space wrap>
                  <Tag color="green">来自插件市场</Tag>
                  <Typography.Text type="tertiary">
                    来源商品：{marketSource.sourceName || `商品#${marketSource.sourceProductId}`}，导入时间：{formatDate(marketSource.importedAt)}
                  </Typography.Text>
                </Space>
              ) : null}
              <Space wrap>
                <Button onClick={onOpenLibrary}>在资源库中管理</Button>
                <Button theme="solid" type="primary" onClick={() => onOpenDetail(item.id)}>打开详情</Button>
                <Button theme="light" onClick={onOpenExplore}>查看市场</Button>
              </Space>
            </article>
          )}}
        />
      </div>
    </Surface>
  );
}

export function PluginDetailPage({
  api,
  locale,
  pluginId,
  onOpenLibrary,
  onOpenExplore
}: StudioPageProps & {
  pluginId: number;
  onOpenLibrary: () => void;
  onOpenExplore: () => void;
}) {
  const [detail, setDetail] = useState<Awaited<ReturnType<StudioPageProps["api"]["getPluginDetail"]>> | null>(null);
  const [publishing, setPublishing] = useState(false);
  const [importedPlugins, setImportedPlugins] = useState<Record<string, ExploreImportedPluginState>>(() => readExploreImportedPluginStateMap());

  async function load() {
    await api.getPluginDetail(pluginId).then(setDetail).catch(error => {
      Toast.error(error instanceof Error ? error.message : "加载插件详情失败。");
    });
  }

  useEffect(() => {
    void load();
  }, [api, pluginId]);

  useEffect(() => {
    setImportedPlugins(readExploreImportedPluginStateMap());
  }, [pluginId]);

  async function handlePublish() {
    setPublishing(true);
    try {
      await api.publishPlugin(pluginId);
      await load();
      Toast.success("插件已发布。");
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "发布插件失败。");
    } finally {
      setPublishing(false);
    }
  }

  const enabledApiCount = detail?.apis.filter(item => item.isEnabled).length ?? 0;
  const openApiText = detail?.openApiSpecJson?.trim() ?? "";
  const authConfigText = detail?.authConfigJson?.trim() ?? "";
  const toolSchemaText = detail?.toolSchemaJson?.trim() ?? "";
  const definitionText = detail?.definitionJson?.trim() ?? "";
  const openApiLineCount = openApiText ? openApiText.split(/\r?\n/).length : 0;
  const authModeLabel = detail ? ({
    0: "无鉴权",
    1: "API Key",
    2: "Bearer",
    3: "OAuth",
    4: "自定义"
  }[detail.authType] ?? `类型 ${detail.authType}`) : "-";
  const sourceTypeLabel = detail ? ({
    0: "手工定义",
    1: "OpenAPI 导入",
    2: "内置插件"
  }[detail.sourceType] ?? `类型 ${detail.sourceType}`) : "-";
  const marketSource = Object.values(importedPlugins).find(source => source.importedPluginId === pluginId);
  const classificationTag = resolvePluginClassificationTag(detail?.sourceType, Boolean(marketSource));

  return (
    <Surface title="插件详情" subtitle="查看插件 API 与基础信息。" testId="app-studio-plugin-detail-page">
      {detail ? (
        <div className="module-studio__chat-layout">
          <section className="module-studio__coze-agent-panel">
            <div className="module-studio__section-head">
              <div>
                <Typography.Title heading={5} style={{ margin: 0 }}>{detail.name}</Typography.Title>
                <Typography.Text type="tertiary">{detail.description || detail.category || "插件能力"}</Typography.Text>
              </div>
              <Space>
                <Tag color="orange">Plugin</Tag>
                <Tag color={classificationTag.color}>{classificationTag.label}</Tag>
                <Tag color={detail.status === 1 ? "green" : "blue"}>{detail.status === 1 ? "Published" : "Draft"}</Tag>
                <Tag color={detail.isLocked ? "red" : "grey"}>{detail.isLocked ? "Locked" : "Unlocked"}</Tag>
              </Space>
            </div>
            <div className="module-studio__coze-inspector-card">
              <Descriptions
                size="small"
                align="left"
                data={[
                  { key: "id", value: detail.id },
                  { key: "category", value: detail.category || "-" },
                  { key: "apiCount", value: detail.apis.length },
                  { key: "enabledApiCount", value: enabledApiCount },
                  { key: "authType", value: authModeLabel },
                  { key: "sourceType", value: sourceTypeLabel },
                  { key: "publishedAt", value: formatDate(detail.publishedAt) },
                  { key: "updatedAt", value: formatDate(detail.updatedAt || detail.createdAt) }
                ]}
              />
            </div>
            {marketSource ? (
              <div className="module-studio__coze-inspector-card">
                <div className="module-studio__card-head">
                  <strong>市场来源</strong>
                  <Tag color="green">来自插件市场</Tag>
                </div>
                <Typography.Text type="tertiary">
                  来源商品：{marketSource.sourceName || `商品#${marketSource.sourceProductId}`}，导入时间：{formatDate(marketSource.importedAt)}
                </Typography.Text>
              </div>
            ) : null}
            <div className="module-studio__coze-inspector-card">
              <div className="module-studio__card-head">
                <strong>发布与规格摘要</strong>
                <Space>
                  <Tag color={openApiText ? "green" : "grey"}>{openApiText ? `OpenAPI ${openApiLineCount} 行` : "无 OpenAPI 规格"}</Tag>
                  <Tag color={authConfigText ? "cyan" : "grey"}>{authConfigText ? "已配置鉴权" : "未配置鉴权"}</Tag>
                  <Tag color={toolSchemaText ? "violet" : "grey"}>{toolSchemaText ? "含工具 Schema" : "无工具 Schema"}</Tag>
                </Space>
              </div>
              <Typography.Text type="tertiary">
                这里汇总插件当前的发布态、来源类型、鉴权方式以及 OpenAPI/Tool/Definition 配置是否齐备。
              </Typography.Text>
            </div>
            <ResourceReferenceCard api={api} locale={locale} resourceType="plugin" resourceId={String(pluginId)} />
            <CardGrid
              testId="app-studio-plugin-apis-grid"
              items={detail.apis}
              render={(apiItem) => (
                <article key={apiItem.id} className="module-studio__coze-card">
                  <div className="module-studio__card-head">
                    <strong>{apiItem.name}</strong>
                    <Tag color={apiItem.isEnabled ? "green" : "grey"}>{apiItem.isEnabled ? "启用" : "停用"}</Tag>
                  </div>
                  <p>{apiItem.method} {apiItem.path}</p>
                  <p>Timeout: {apiItem.timeoutSeconds}s</p>
                  <pre className="module-studio__message-content">{apiItem.requestSchemaJson}</pre>
                </article>
              )}
            />
          </section>
          <aside className="module-studio__coze-agent-preview">
            <div className="module-studio__coze-inspector-card">
              <Space wrap>
                <Button theme="solid" type="primary" loading={publishing} onClick={() => void handlePublish()}>
                  发布插件
                </Button>
                <Button theme="solid" type="primary" onClick={onOpenLibrary}>进入资源库</Button>
                <Button onClick={onOpenExplore}>打开插件市场</Button>
              </Space>
            </div>
            {openApiText ? (
              <div className="module-studio__coze-inspector-card">
                <div className="module-studio__card-head">
                  <strong>OpenAPI 摘要</strong>
                  <Tag color="green">Spec</Tag>
                </div>
                <pre className="module-studio__message-content">{tryFormatJson(openApiText)}</pre>
              </div>
            ) : null}
            {definitionText ? (
              <div className="module-studio__coze-inspector-card">
                <div className="module-studio__card-head">
                  <strong>Definition</strong>
                </div>
                <pre className="module-studio__message-content">{tryFormatJson(definitionText)}</pre>
              </div>
            ) : null}
          </aside>
        </div>
      ) : (
        <Empty title="未找到插件" image={null} />
      )}
    </Surface>
  );
}

export function AppDetailPage(
  props: StudioPageProps & {
    appId: string;
    onOpenWorkflow?: (workflowId: string) => void;
    onOpenPublish?: () => void;
  }
) {
  return <AppBuilderPage {...props} />;
}

export function AppPublishPage({
  api,
  appId
}: StudioPageProps & { appId: string }) {
  const [detail, setDetail] = useState<StudioApplicationSummary | null>(null);
  const [records, setRecords] = useState<StudioApplicationPublishRecord[]>([]);
  const [releaseNote, setReleaseNote] = useState("");
  const [publishing, setPublishing] = useState(false);

  async function load() {
    try {
      const [nextDetail, nextRecords] = await Promise.all([
        api.getApplication(appId),
        api.getApplicationPublishRecords(appId)
      ]);
      setDetail(nextDetail);
      setRecords(nextRecords);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "加载应用发布页失败。");
    }
  }

  useEffect(() => {
    void load();
  }, [api, appId]);

  async function handlePublish() {
    setPublishing(true);
    try {
      await api.publishApplication(appId, releaseNote.trim() || undefined);
      Toast.success("应用已发布。");
      setReleaseNote("");
      await load();
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "发布应用失败。");
    } finally {
      setPublishing(false);
    }
  }

  return (
    <Surface title="应用发布" subtitle="版本说明、发布动作与历史记录。" testId="app-studio-app-publish-page">
      <div className="module-studio__stack">
        {detail ? (
          <div className="module-studio__coze-inspector-card">
            <div className="module-studio__card-head">
              <strong>{detail.name}</strong>
              <Tag color={detail.status?.toLowerCase() === "published" ? "green" : "blue"}>{detail.status || "Draft"}</Tag>
            </div>
            <Typography.Text type="tertiary">{detail.description || "当前应用还没有补充描述。"}</Typography.Text>
            <span className="module-studio__meta">当前版本：v{detail.publishVersion ?? 0}</span>
          </div>
        ) : null}
        <div className="module-studio__coze-inspector-card">
          <span>发布说明</span>
          <textarea
            rows={4}
            className="module-studio__textarea"
            value={releaseNote}
            onChange={event => setReleaseNote(event.target.value)}
            placeholder="输入发布说明，可选。"
          />
          <Button theme="solid" type="primary" loading={publishing} onClick={() => void handlePublish()}>
            发布应用
          </Button>
        </div>
        <div className="module-studio__coze-inspector-card">
          <div className="module-studio__card-head">
            <span>历史发布</span>
            <Tag color="light-blue">{records.length}</Tag>
          </div>
          {records.length === 0 ? (
            <Typography.Text type="tertiary">当前还没有发布记录。</Typography.Text>
          ) : (
            <div className="module-studio__stack">
              {records.map(item => (
                <div key={item.id} className="module-studio__coze-linkrow">
                  <div>
                    <strong>{item.version}</strong>
                    <div className="module-studio__meta">{item.releaseNote || "无发布说明"}</div>
                  </div>
                  <span>{formatDate(item.createdAt)}</span>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </Surface>
  );
}

export function AssistantPublishPage({
  api,
  assistantId
}: StudioPageProps & { assistantId: string }) {
  const [detail, setDetail] = useState<AgentDetail | null>(null);
  const [records, setRecords] = useState<StudioAssistantPublication[]>([]);
  const [releaseNote, setReleaseNote] = useState("");
  const [publishing, setPublishing] = useState(false);

  async function load() {
    try {
      const [nextDetail, nextRecords] = await Promise.all([
        api.getAgent(assistantId),
        api.getAgentPublications(assistantId)
      ]);
      setDetail(nextDetail);
      setRecords(nextRecords);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "加载智能体发布页失败。");
    }
  }

  useEffect(() => {
    void load();
  }, [api, assistantId]);

  async function handlePublish() {
    setPublishing(true);
    try {
      await api.publishAgent(assistantId, releaseNote.trim() || undefined);
      Toast.success("智能体已发布。");
      setReleaseNote("");
      await load();
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "发布智能体失败。");
    } finally {
      setPublishing(false);
    }
  }

  async function handleRegenerateToken() {
    try {
      await api.regenerateAgentEmbedToken(assistantId);
      Toast.success("Embed token 已刷新。");
      await load();
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "刷新 Embed token 失败。");
    }
  }

  return (
    <Surface title="智能体发布" subtitle="版本、嵌入令牌与历史发布。" testId="app-studio-assistant-publish-page">
      <div className="module-studio__stack">
        {detail ? (
          <div className="module-studio__coze-inspector-card">
            <div className="module-studio__card-head">
              <strong>{detail.name}</strong>
              <Tag color={detail.status?.toLowerCase() === "published" ? "green" : "blue"}>{detail.status || "Draft"}</Tag>
            </div>
            <Typography.Text type="tertiary">{detail.description || "当前智能体还没有补充描述。"}</Typography.Text>
          </div>
        ) : null}
        <div className="module-studio__coze-inspector-card">
          <span>发布说明</span>
          <textarea
            rows={4}
            className="module-studio__textarea"
            value={releaseNote}
            onChange={event => setReleaseNote(event.target.value)}
            placeholder="输入发布说明，可选。"
          />
          <Space wrap>
            <Button theme="solid" type="primary" loading={publishing} onClick={() => void handlePublish()}>
              发布智能体
            </Button>
            <Button onClick={() => void handleRegenerateToken()} disabled={records.length === 0}>
              刷新 Embed Token
            </Button>
          </Space>
        </div>
        <div className="module-studio__coze-inspector-card">
          <div className="module-studio__card-head">
            <span>历史发布</span>
            <Tag color="cyan">{records.length}</Tag>
          </div>
          {records.length === 0 ? (
            <Typography.Text type="tertiary">当前还没有发布记录。</Typography.Text>
          ) : (
            <div className="module-studio__stack">
              {records.map(item => (
                <div key={item.id} className="module-studio__coze-linkrow">
                  <div>
                    <strong>v{item.version}</strong>
                    <div className="module-studio__meta">
                      {item.isActive ? "当前激活" : "历史版本"} / {formatDate(item.createdAt)}
                    </div>
                    <div className="module-studio__meta">
                      Token: {item.embedToken ? `${item.embedToken.slice(0, 12)}...` : "-"}
                    </div>
                  </div>
                  <Tag color={item.isActive ? "green" : "grey"}>
                    {item.isActive ? "Active" : "Archived"}
                  </Tag>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </Surface>
  );
}

export function VariablesPage({ api }: StudioPageProps) {
  const [items, setItems] = useState<StudioVariableItem[]>([]);
  const [systemItems, setSystemItems] = useState<StudioSystemVariableDefinition[]>([]);
  const [keyword, setKeyword] = useState("");
  const [scope, setScope] = useState<number | "">("");
  const [draft, setDraft] = useState<StudioVariableCreateRequest>({
    key: "",
    value: "",
    scope: 1
  });
  const [editingId, setEditingId] = useState<number | null>(null);
  const [saving, setSaving] = useState(false);

  async function load() {
    try {
      const [paged, systemDefs] = await Promise.all([
        api.listVariables({
          pageIndex: 1,
          pageSize: 100,
          keyword: keyword.trim() || undefined,
          scope: scope === "" ? undefined : scope
        }),
        api.listSystemVariables()
      ]);
      setItems(paged.items);
      setSystemItems(systemDefs);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "加载变量失败。");
    }
  }

  useEffect(() => {
    void load();
  }, [api, keyword, scope]);

  function resetDraft() {
    setDraft({
      key: "",
      value: "",
      scope: 1
    });
    setEditingId(null);
  }

  async function handleSave() {
    if (!draft.key.trim()) {
      Toast.warning("请先填写变量键。");
      return;
    }

    setSaving(true);
    try {
      if (editingId) {
        await api.updateVariable(editingId, {
          ...draft,
          key: draft.key.trim(),
          value: draft.value?.trim() || undefined
        });
        Toast.success("变量已更新。");
      } else {
        await api.createVariable({
          ...draft,
          key: draft.key.trim(),
          value: draft.value?.trim() || undefined
        });
        Toast.success("变量已创建。");
      }
      resetDraft();
      await load();
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "保存变量失败。");
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(id: number) {
    try {
      await api.deleteVariable(id);
      Toast.success("变量已删除。");
      await load();
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "删除变量失败。");
    }
  }

  return (
    <Surface
      title="变量中心"
      subtitle="统一管理应用、智能体和系统变量。"
      testId="app-studio-variables-page"
      toolbar={
        <Space>
          <Input
            value={keyword}
            onChange={setKeyword}
            placeholder="搜索变量键"
            showClear
            data-testid="app-studio-variables-search"
          />
          <Select
            value={scope}
            placeholder="作用域"
            optionList={[
              { label: "全部", value: "" },
              { label: "系统", value: 0 },
              { label: "应用", value: 1 },
              { label: "智能体", value: 2 }
            ]}
            onChange={value => setScope(typeof value === "number" ? value : "")}
          />
        </Space>
      }
    >
      <div className="module-studio__chat-layout">
        <section className="module-studio__coze-agent-panel">
          <div className="module-studio__section-head">
            <div>
              <Typography.Title heading={5} style={{ margin: 0 }}>变量列表</Typography.Title>
              <Typography.Text type="tertiary">按作用域查看当前可编辑变量。</Typography.Text>
            </div>
            <Button theme="borderless" onClick={resetDraft}>重置表单</Button>
          </div>
          <CardGrid
            testId="app-studio-variables-grid"
            items={items}
            render={(item: StudioVariableItem) => (
              <article key={item.id} className="module-studio__coze-card">
                <div className="module-studio__card-head">
                  <div>
                    <Tag color={item.scope === 0 ? "orange" : item.scope === 1 ? "blue" : "cyan"}>
                      {item.scope === 0 ? "系统" : item.scope === 1 ? "应用" : "智能体"}
                    </Tag>
                    <strong>{item.key}</strong>
                  </div>
                  <span className="module-studio__meta">{formatDate(item.updatedAt || item.createdAt)}</span>
                </div>
                <p>{item.value || "无默认值"}</p>
                <Space wrap>
                  <Button
                    onClick={() => {
                      setEditingId(item.id);
                      setDraft({
                        key: item.key,
                        value: item.value,
                        scope: item.scope,
                        scopeId: item.scopeId
                      });
                    }}
                  >
                    编辑
                  </Button>
                  <Button type="danger" theme="borderless" onClick={() => void handleDelete(item.id)}>
                    删除
                  </Button>
                </Space>
              </article>
            )}
          />
        </section>

        <aside className="module-studio__coze-agent-preview">
          <div className="module-studio__coze-inspector-card">
            <Typography.Title heading={6}>新建 / 编辑变量</Typography.Title>
            <div className="module-studio__stack">
              <Input
                value={draft.key}
                onChange={value => setDraft(current => ({ ...current, key: value }))}
                placeholder="变量键"
              />
              <Input
                value={draft.value ?? ""}
                onChange={value => setDraft(current => ({ ...current, value }))}
                placeholder="默认值"
              />
              <Select
                value={draft.scope}
                optionList={[
                  { label: "系统", value: 0 },
                  { label: "应用", value: 1 },
                  { label: "智能体", value: 2 }
                ]}
                onChange={value => setDraft(current => ({ ...current, scope: Number(value) }))}
              />
              <InputNumber
                value={draft.scopeId}
                onNumberChange={value => setDraft(current => ({ ...current, scopeId: typeof value === "number" ? value : undefined }))}
                placeholder="作用域 ID（可选）"
              />
              <Button theme="solid" type="primary" loading={saving} onClick={() => void handleSave()}>
                {editingId ? "保存变量" : "创建变量"}
              </Button>
            </div>
          </div>

          <div className="module-studio__coze-inspector-card">
            <div className="module-studio__card-head">
              <span>系统变量定义</span>
              <Tag color="light-blue">{systemItems.length}</Tag>
            </div>
            {systemItems.length === 0 ? (
              <Typography.Text type="tertiary">当前没有系统变量定义。</Typography.Text>
            ) : (
              <div className="module-studio__stack">
                {systemItems.map(item => (
                  <div key={item.key} className="module-studio__coze-linkrow">
                    <div>
                      <strong>{item.key}</strong>
                      <div className="module-studio__meta">{item.name}</div>
                      <div className="module-studio__meta">{item.description}</div>
                    </div>
                    <Tag color="orange">System</Tag>
                  </div>
                ))}
              </div>
            )}
          </div>
        </aside>
      </div>
    </Surface>
  );
}

export function DataResourcesPage({
  api,
  onOpenLibrary
}: StudioPageProps & {
  onOpenLibrary: () => void;
}) {
  const [knowledgeItems, setKnowledgeItems] = useState<Array<{ id: number; name: string; type: number }>>([]);
  const [databaseItems, setDatabaseItems] = useState<Array<{ id: number; name: string; botId?: number }>>([]);

  useEffect(() => {
    void Promise.all([
      api.listKnowledgeBases(),
      api.listDatabases()
    ]).then(([knowledge, databases]) => {
      setKnowledgeItems(knowledge);
      setDatabaseItems(databases);
    }).catch(error => {
      Toast.error(error instanceof Error ? error.message : "加载数据资源失败。");
    });
  }, [api]);

  return (
    <Surface title="数据资源" subtitle="知识库与数据库统一入口。" testId="app-studio-data-page">
      <div className="module-studio__chat-layout">
        <section className="module-studio__coze-agent-panel">
          <div className="module-studio__section-head">
            <div>
              <Typography.Title heading={5} style={{ margin: 0 }}>知识库</Typography.Title>
              <Typography.Text type="tertiary">管理文本、表格和图像知识库。</Typography.Text>
            </div>
            <Button onClick={onOpenLibrary}>进入资源库</Button>
          </div>
          <CardGrid
            testId="app-studio-data-knowledge-grid"
            items={knowledgeItems}
            render={(item) => (
              <article key={item.id} className="module-studio__coze-card">
                <div className="module-studio__card-head">
                  <div>
                    <Tag color="green">知识库</Tag>
                    <strong>{item.name}</strong>
                  </div>
                  <span className="module-studio__meta">type:{item.type}</span>
                </div>
                <p>统一的知识检索与文档处理入口。</p>
                <Button onClick={onOpenLibrary}>查看详情</Button>
              </article>
            )}
          />
        </section>

        <aside className="module-studio__coze-agent-preview">
          <div className="module-studio__section-head">
            <div>
              <Typography.Title heading={5} style={{ margin: 0 }}>数据库</Typography.Title>
              <Typography.Text type="tertiary">结构化表和导入任务管理。</Typography.Text>
            </div>
            <Button onClick={onOpenLibrary}>进入资源库</Button>
          </div>
          <CardGrid
            testId="app-studio-data-database-grid"
            items={databaseItems}
            render={(item) => (
              <article key={item.id} className="module-studio__coze-card">
                <div className="module-studio__card-head">
                  <div>
                    <Tag color="blue">数据库</Tag>
                    <strong>{item.name}</strong>
                  </div>
                  <span className="module-studio__meta">{item.botId ? `Bot:${item.botId}` : "未绑定"}</span>
                </div>
                <p>结构化数据表、记录和导入模板。</p>
                <Button onClick={onOpenLibrary}>查看详情</Button>
              </article>
            )}
          />
        </aside>
      </div>
    </Surface>
  );
}

export function KnowledgeBasesPage({
  api,
  onOpenDetail,
  onOpenUpload,
  onOpenLibrary
}: StudioPageProps & {
  onOpenDetail: (knowledgeBaseId: number) => void;
  onOpenUpload: (knowledgeBaseId: number, type: number) => void;
  onOpenLibrary: () => void;
}) {
  const [items, setItems] = useState<Array<{ id: number; name: string; type: number }>>([]);
  const [keyword, setKeyword] = useState("");

  useEffect(() => {
    void api.listKnowledgeBases().then(result => {
      const normalizedKeyword = keyword.trim().toLowerCase();
      const filtered = normalizedKeyword
        ? result.filter(item => item.name.toLowerCase().includes(normalizedKeyword))
        : result;
      setItems(filtered);
    }).catch(error => {
      Toast.error(error instanceof Error ? error.message : "加载知识库失败。");
    });
  }, [api, keyword]);

  return (
    <Surface
      title="知识库中心"
      subtitle="统一管理文本、表格和图像知识库。"
      testId="app-studio-knowledge-bases-page"
      toolbar={
        <Input
          value={keyword}
          onChange={setKeyword}
          placeholder="搜索知识库"
          showClear
          data-testid="app-studio-knowledge-search"
        />
      }
    >
      <div className="module-studio__stack">
        <Space>
          <Button theme="solid" type="primary" onClick={onOpenLibrary}>进入资源库</Button>
        </Space>
        <CardGrid
          testId="app-studio-knowledge-grid"
          items={items}
          render={(item) => (
            <article key={item.id} className="module-studio__coze-card">
              <div className="module-studio__card-head">
                <div>
                  <Tag color="green">知识库</Tag>
                  <strong>{item.name}</strong>
                </div>
                <span className="module-studio__meta">type:{item.type}</span>
              </div>
              <p>查看知识文档、分片和检索测试。</p>
              <Space wrap>
                <Button theme="solid" type="primary" onClick={() => onOpenDetail(item.id)}>
                  打开详情
                </Button>
                <Button onClick={() => onOpenUpload(item.id, item.type)}>
                  上传文档
                </Button>
                <Button onClick={onOpenLibrary}>进入资源库</Button>
              </Space>
            </article>
          )}
        />
      </div>
    </Surface>
  );
}

export function DatabaseDetailPage({
  api,
  locale,
  databaseId,
  onOpenLibrary
}: StudioPageProps & {
  databaseId: number;
  onOpenLibrary: () => void;
}) {
  const [detail, setDetail] = useState<StudioDatabaseDetail | null>(null);
  const [records, setRecords] = useState<StudioDatabaseRecordItem[]>([]);
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize] = useState(10);
  const [total, setTotal] = useState(0);
  const [loadingRecords, setLoadingRecords] = useState(false);
  const [editingRecordId, setEditingRecordId] = useState<number | null>(null);
  const [recordDialogVisible, setRecordDialogVisible] = useState(false);
  const [recordDraft, setRecordDraft] = useState("{\n  \n}");
  const [recordSaving, setRecordSaving] = useState(false);
  const [schemaChecking, setSchemaChecking] = useState(false);
  const [schemaValidation, setSchemaValidation] = useState<{ isValid: boolean; errors: string[] } | null>(null);
  const [importProgress, setImportProgress] = useState<Awaited<ReturnType<StudioPageProps["api"]["getDatabaseImportProgress"]>>>(null);
  const [importing, setImporting] = useState(false);
  const importInputRef = useRef<HTMLInputElement | null>(null);
  const [bulkDialogVisible, setBulkDialogVisible] = useState(false);
  const [bulkDraft, setBulkDraft] = useState('{"orderId":"DEMO-1","amount":1}\n');
  const [bulkAsync, setBulkAsync] = useState(false);
  const [bulkSubmitting, setBulkSubmitting] = useState(false);

  const loadDetail = async () => {
    try {
      const nextDetail = await api.getDatabaseDetail(databaseId);
      setDetail(nextDetail);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "加载数据库详情失败。");
    }
  };

  const loadRecords = async (currentPageIndex: number) => {
    setLoadingRecords(true);
    try {
      const result = await api.listDatabaseRecords(databaseId, {
        pageIndex: currentPageIndex,
        pageSize
      });
      setRecords(result.items);
      setTotal(result.total);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "加载数据库记录失败。");
    } finally {
      setLoadingRecords(false);
    }
  };

  const loadImportProgress = async () => {
    try {
      const result = await api.getDatabaseImportProgress(databaseId);
      setImportProgress(result);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "加载导入进度失败。");
    }
  };

  useEffect(() => {
    void loadDetail();
  }, [api, databaseId]);

  useEffect(() => {
    void loadRecords(pageIndex);
  }, [api, databaseId, pageIndex, pageSize]);

  useEffect(() => {
    void loadImportProgress();
  }, [api, databaseId]);

  const totalPages = Math.max(1, Math.ceil(total / pageSize));

  const openCreateRecordDialog = () => {
    setEditingRecordId(null);
    setRecordDraft("{\n  \n}");
    setRecordDialogVisible(true);
  };

  const openEditRecordDialog = (record: StudioDatabaseRecordItem) => {
    setEditingRecordId(record.id);
    setRecordDraft(tryFormatJson(record.dataJson));
    setRecordDialogVisible(true);
  };

  const handleSaveRecord = async () => {
    let normalizedJson = "{}";
    try {
      const parsed = JSON.parse(recordDraft);
      if (!isRecord(parsed)) {
        Toast.error("数据库记录必须是 JSON 对象。");
        return;
      }

      normalizedJson = JSON.stringify(parsed);
    } catch {
      Toast.error("记录 JSON 格式不合法。");
      return;
    }

    setRecordSaving(true);
    try {
      if (editingRecordId) {
        await api.updateDatabaseRecord(databaseId, editingRecordId, {
          dataJson: normalizedJson
        });
        Toast.success("数据库记录已更新。");
      } else {
        await api.createDatabaseRecord(databaseId, {
          dataJson: normalizedJson
        });
        Toast.success("数据库记录已创建。");
      }

      setRecordDialogVisible(false);
      await Promise.all([loadDetail(), loadRecords(pageIndex)]);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "保存数据库记录失败。");
    } finally {
      setRecordSaving(false);
    }
  };

  const handleDeleteRecord = async (recordId: number) => {
    if (!window.confirm("确认删除这条数据库记录吗？")) {
      return;
    }

    try {
      await api.deleteDatabaseRecord(databaseId, recordId);
      const nextTotal = Math.max(0, total - 1);
      const nextPageIndex = nextTotal > 0 && pageIndex > Math.ceil(nextTotal / pageSize)
        ? Math.max(1, pageIndex - 1)
        : pageIndex;
      setPageIndex(nextPageIndex);
      await Promise.all([loadDetail(), loadRecords(nextPageIndex)]);
      Toast.success("数据库记录已删除。");
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "删除数据库记录失败。");
    }
  };

  const handleValidateSchema = async () => {
    if (!detail) {
      return;
    }

    setSchemaChecking(true);
    try {
      const result = await api.validateDatabaseSchemaDraft(detail.tableSchema);
      setSchemaValidation(result);
      Toast.success(result.isValid ? "Schema 校验通过。" : "Schema 校验未通过。");
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "校验数据库 Schema 失败。");
    } finally {
      setSchemaChecking(false);
    }
  };

  const handleDownloadTemplate = async () => {
    try {
      await api.downloadDatabaseTemplate(databaseId);
      Toast.success("模板下载已开始。");
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "下载数据库模板失败。");
    }
  };

  const handlePickImportFile = () => {
    importInputRef.current?.click();
  };

  const handleImportFileChange = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) {
      return;
    }

    setImporting(true);
    try {
      const taskId = await api.submitDatabaseImport(databaseId, file);
      await Promise.all([loadImportProgress(), loadDetail()]);
      Toast.success(`导入任务已提交，任务号 ${taskId}。`);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "提交导入任务失败。");
    } finally {
      setImporting(false);
    }
  };

  const openBulkDialog = () => {
    setBulkDraft('{"orderId":"DEMO-1","amount":1}\n');
    setBulkAsync(false);
    setBulkDialogVisible(true);
  };

  const handleSubmitBulk = async () => {
    if (!api.bulkCreateDatabaseRecords && !api.submitDatabaseBulkInsertJob) {
      Toast.error("当前环境未启用批量插入 API。");
      return;
    }
    const lines = bulkDraft.split(/\r?\n/).map(l => l.trim()).filter(Boolean);
    if (lines.length === 0) {
      Toast.error("请至少输入一行 JSON 对象。");
      return;
    }
    const rows: string[] = [];
    for (let i = 0; i < lines.length; i++) {
      try {
        const parsed = JSON.parse(lines[i] ?? "");
        if (!isRecord(parsed)) {
          Toast.error(`第 ${i + 1} 行：必须是 JSON 对象。`);
          return;
        }
        rows.push(JSON.stringify(parsed));
      } catch {
        Toast.error(`第 ${i + 1} 行：JSON 格式无效。`);
        return;
      }
    }
    setBulkSubmitting(true);
    try {
      if (bulkAsync) {
        if (!api.submitDatabaseBulkInsertJob) {
          Toast.error("异步批量接口不可用。");
          return;
        }
        const job = await api.submitDatabaseBulkInsertJob(databaseId, { rows });
        Toast.success(`异步任务已提交，taskId=${job.taskId}，行数=${job.rowCount}。`);
      } else {
        if (!api.bulkCreateDatabaseRecords) {
          Toast.error("同步批量接口不可用。");
          return;
        }
        const result = await api.bulkCreateDatabaseRecords(databaseId, { rows });
        Toast.success(`批量完成：成功 ${result.succeeded}，失败 ${result.failed}。`);
      }
      setBulkDialogVisible(false);
      await Promise.all([loadDetail(), loadRecords(pageIndex), loadImportProgress()]);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "批量插入失败。");
    } finally {
      setBulkSubmitting(false);
    }
  };

  return (
    <Surface title="数据库详情" subtitle="查看数据库结构与绑定信息。" testId="app-studio-database-detail-page">
      {detail ? (
        <div className="module-studio__stack">
          <div className="module-studio__coze-inspector-card">
            <div className="module-studio__card-head">
              <strong>{detail.name}</strong>
              <Tag color="blue">数据库</Tag>
            </div>
            <Typography.Text type="tertiary">{detail.description || "当前数据库还没有补充描述。"}</Typography.Text>
            <Descriptions
              size="small"
              align="left"
              data={[
                { key: "id", value: detail.id },
                { key: "recordCount", value: detail.recordCount },
                { key: "botId", value: detail.botId || "-" },
                { key: "updatedAt", value: formatDate(detail.updatedAt || detail.createdAt) }
              ]}
            />
            <Space wrap>
              <Button onClick={onOpenLibrary}>进入资源库</Button>
              <Button theme="solid" type="primary" icon={<IconPlus />} onClick={openCreateRecordDialog}>
                新增记录
              </Button>
              {api.bulkCreateDatabaseRecords || api.submitDatabaseBulkInsertJob ? (
                <Button onClick={openBulkDialog}>批量插入</Button>
              ) : null}
              <Button onClick={() => void handleDownloadTemplate()}>
                下载模板
              </Button>
              <Button loading={importing} onClick={handlePickImportFile}>
                导入数据
              </Button>
            </Space>
            <input
              ref={importInputRef}
              type="file"
              accept=".csv,text/csv"
              style={{ display: "none" }}
              onChange={event => void handleImportFileChange(event)}
            />
          </div>
          <ResourceReferenceCard api={api} locale={locale} resourceType="database" resourceId={String(databaseId)} />
          <div className="module-studio__coze-inspector-card">
            <div className="module-studio__card-head">
              <strong>表结构</strong>
              <Space>
                <Button loading={schemaChecking} onClick={() => void handleValidateSchema()}>
                  校验当前 Schema
                </Button>
                {schemaValidation ? (
                  <Tag color={schemaValidation.isValid ? "green" : "red"}>
                    {schemaValidation.isValid ? "Valid" : `Invalid · ${schemaValidation.errors.length}`}
                  </Tag>
                ) : null}
              </Space>
            </div>
            <pre className="module-studio__message-content">{tryFormatJson(detail.tableSchema)}</pre>
            {schemaValidation && !schemaValidation.isValid ? (
              <div className="module-studio__stack">
                {schemaValidation.errors.map(error => (
                  <Banner key={error} type="danger" bordered={false} closeIcon={null} description={error} />
                ))}
              </div>
            ) : null}
          </div>
          <div className="module-studio__coze-inspector-card">
            <div className="module-studio__card-head">
              <strong>最近导入任务</strong>
              <Space>
                <Button onClick={() => void loadImportProgress()}>
                  刷新进度
                </Button>
                {importProgress ? (
                  <Tag color={
                    importProgress.status === 2 ? "green"
                      : importProgress.status === 3 ? "red"
                      : importProgress.status === 1 ? "blue"
                      : "grey"
                  }>
                    {importProgress.status === 0
                      ? "Pending"
                      : importProgress.status === 1
                        ? "Running"
                        : importProgress.status === 2
                          ? "Completed"
                          : "Failed"}
                  </Tag>
                ) : null}
              </Space>
            </div>
            {importProgress ? (
              <div className="module-studio__stack">
                <Descriptions
                  size="small"
                  align="left"
                  data={[
                    { key: "taskId", value: importProgress.taskId },
                    {
                      key: "source",
                      value:
                        importProgress.source === 1
                          ? "Inline JSON（异步批量）"
                          : importProgress.source === 0
                            ? "CSV 文件"
                            : importProgress.source ?? "-"
                    },
                    { key: "totalRows", value: importProgress.totalRows },
                    { key: "succeededRows", value: importProgress.succeededRows },
                    { key: "failedRows", value: importProgress.failedRows },
                    { key: "updatedAt", value: formatDate(importProgress.updatedAt || importProgress.createdAt) }
                  ]}
                />
                {importProgress.errorMessage ? (
                  <Banner type="danger" bordered={false} closeIcon={null} description={importProgress.errorMessage} />
                ) : (
                  <Typography.Text type="tertiary">
                    {importProgress.source === 1
                      ? "此任务来自内联 JSON 异步批量。"
                      : "CSV 会先上传文件，再异步写入记录表。"}
                  </Typography.Text>
                )}
              </div>
            ) : (
              <Empty title="暂无导入任务" image={null} />
            )}
          </div>
          <div className="module-studio__coze-inspector-card">
            <div className="module-studio__card-head">
              <strong>数据记录</strong>
              <Typography.Text type="tertiary">
                第 {pageIndex} / {totalPages} 页，共 {total} 条
              </Typography.Text>
            </div>
            {loadingRecords ? (
              <Typography.Text type="tertiary">正在加载数据库记录...</Typography.Text>
            ) : records.length === 0 ? (
              <Empty title="暂无数据库记录" image={null} />
            ) : (
              <div className="module-studio__stack">
                {records.map(record => (
                  <article key={record.id} className="module-studio__coze-card">
                    <div className="module-studio__card-head">
                      <div>
                        <Tag color="cyan">Record</Tag>
                        <strong>#{record.id}</strong>
                      </div>
                      <Typography.Text type="tertiary">
                        {formatDate(record.updatedAt || record.createdAt)}
                      </Typography.Text>
                    </div>
                    <pre className="module-studio__message-content">{tryFormatJson(record.dataJson)}</pre>
                    <Space wrap>
                      <Button onClick={() => openEditRecordDialog(record)}>编辑</Button>
                      <Button type="danger" theme="borderless" onClick={() => void handleDeleteRecord(record.id)}>
                        删除
                      </Button>
                    </Space>
                  </article>
                ))}
                <Space>
                  <Button disabled={pageIndex <= 1} onClick={() => setPageIndex(current => Math.max(1, current - 1))}>
                    上一页
                  </Button>
                  <Button disabled={pageIndex >= totalPages} onClick={() => setPageIndex(current => Math.min(totalPages, current + 1))}>
                    下一页
                  </Button>
                </Space>
              </div>
            )}
          </div>
        </div>
      ) : (
        <Empty title="未找到数据库" image={null} />
      )}
      <Modal
        title={editingRecordId ? `编辑记录 #${editingRecordId}` : "新增数据库记录"}
        visible={recordDialogVisible}
        onCancel={() => {
          if (!recordSaving) {
            setRecordDialogVisible(false);
          }
        }}
        onOk={() => void handleSaveRecord()}
        okText={editingRecordId ? "保存" : "创建"}
        cancelText="取消"
        confirmLoading={recordSaving}
      >
        <div className="module-studio__stack">
          <Typography.Text type="tertiary">
            请输入 JSON 对象作为记录内容，字段应与当前表结构保持一致。
          </Typography.Text>
          <textarea
            rows={14}
            className="module-studio__textarea"
            value={recordDraft}
            onChange={event => setRecordDraft(event.target.value)}
          />
        </div>
      </Modal>
      <Modal
        title="批量插入记录"
        visible={bulkDialogVisible}
        onCancel={() => {
          if (!bulkSubmitting) {
            setBulkDialogVisible(false);
          }
        }}
        onOk={() => void handleSubmitBulk()}
        okText="提交"
        cancelText="取消"
        confirmLoading={bulkSubmitting}
      >
        <div className="module-studio__stack">
          <Typography.Text type="tertiary">
            每行一个 JSON 对象。同步模式受单次批量行数上限限制；异步模式适合更大批量，进度见「最近导入任务」。
          </Typography.Text>
          <div>
            <Typography.Text strong style={{ marginRight: 8 }}>模式</Typography.Text>
            <Space>
              <Button size="small" type={bulkAsync ? "tertiary" : "primary"} onClick={() => setBulkAsync(false)}>
                同步批量
              </Button>
              <Button size="small" type={bulkAsync ? "primary" : "tertiary"} onClick={() => setBulkAsync(true)}>
                异步批量
              </Button>
            </Space>
          </div>
          <textarea
            rows={12}
            className="module-studio__textarea"
            value={bulkDraft}
            onChange={event => setBulkDraft(event.target.value)}
          />
        </div>
      </Modal>
    </Surface>
  );
}

export function DatabasesPage({
  api,
  onOpenDetail,
  onOpenLibrary
}: StudioPageProps & {
  onOpenDetail: (databaseId: number) => void;
  onOpenLibrary: () => void;
}) {
  const [items, setItems] = useState<Array<{ id: number; name: string; botId?: number }>>([]);
  const [keyword, setKeyword] = useState("");

  useEffect(() => {
    void api.listDatabases().then(result => {
      const normalizedKeyword = keyword.trim().toLowerCase();
      const filtered = normalizedKeyword
        ? result.filter(item => item.name.toLowerCase().includes(normalizedKeyword))
        : result;
      setItems(filtered);
    }).catch(error => {
      Toast.error(error instanceof Error ? error.message : "加载数据库失败。");
    });
  }, [api, keyword]);

  return (
    <Surface
      title="数据库中心"
      subtitle="统一管理结构化数据表、记录与导入任务。"
      testId="app-studio-databases-page"
      toolbar={
        <Input
          value={keyword}
          onChange={setKeyword}
          placeholder="搜索数据库"
          showClear
          data-testid="app-studio-databases-search"
        />
      }
    >
      <div className="module-studio__stack">
        <Space>
          <Button theme="solid" type="primary" onClick={onOpenLibrary}>进入资源库</Button>
        </Space>
        <CardGrid
          testId="app-studio-databases-grid"
          items={items}
          render={(item) => (
            <article key={item.id} className="module-studio__coze-card">
              <div className="module-studio__card-head">
                <div>
                  <Tag color="blue">数据库</Tag>
                  <strong>{item.name}</strong>
                </div>
                <span className="module-studio__meta">{item.botId ? `Bot:${item.botId}` : "未绑定"}</span>
              </div>
              <p>查看表结构、记录和导入任务。</p>
              <Space wrap>
                <Button theme="solid" type="primary" onClick={() => onOpenDetail(item.id)}>
                  打开详情
                </Button>
                <Button onClick={onOpenLibrary}>进入资源库</Button>
              </Space>
            </article>
          )}
        />
      </div>
    </Surface>
  );
}

export function ModelConfigsPage({ api, locale }: StudioPageProps) {
  const [items, setItems] = useState<ModelConfigItem[]>([]);
  const [stats, setStats] = useState<ModelConfigStats | null>(null);
  const [keyword, setKeyword] = useState("");
  const [draft, setDraft] = useState<ModelConfigDraft>({ ...DEFAULT_MODEL_CONFIG_DRAFT });
  const [selectedItem, setSelectedItem] = useState<ModelConfigItem | null>(null);
  const [dialogMode, setDialogMode] = useState<"create" | "edit">("create");
  const [modalVisible, setModalVisible] = useState(false);
  const [saving, setSaving] = useState(false);
  const [testResult, setTestResult] = useState<string>("");
  const [prompt, setPrompt] = useState("请介绍这个模型配置适合的场景，并返回三条建议。");
  const [testing, setTesting] = useState(false);

  async function load() {
    const [listResult, statsResult] = await Promise.all([
      api.listModelConfigs(),
      api.getModelConfigStats(keyword || undefined)
    ]);
    setItems(
      listResult.items.filter(item =>
        !keyword ||
        item.name.toLowerCase().includes(keyword.toLowerCase()) ||
        item.providerType.toLowerCase().includes(keyword.toLowerCase()) ||
        item.defaultModel.toLowerCase().includes(keyword.toLowerCase())
      )
    );
    setStats(statsResult);
  }

  useEffect(() => {
    void load();
  }, [api, keyword]);

  function openCreateDialog() {
    setDialogMode("create");
    setSelectedItem(null);
    setDraft({ ...DEFAULT_MODEL_CONFIG_DRAFT });
    setTestResult("");
    setPrompt("请介绍这个模型配置适合的场景，并返回三条建议。");
    setModalVisible(true);
  }

  async function openEditDialog(item: ModelConfigItem) {
    const detail = await api.getModelConfig(item.id);
    setDialogMode("edit");
    setSelectedItem(detail);
    setDraft(toModelConfigDraft(detail));
    setTestResult("");
    setPrompt("请介绍这个模型配置适合的场景，并返回三条建议。");
    setModalVisible(true);
  }

  function closeDialog() {
    if (saving || testing) {
      return;
    }
    setModalVisible(false);
  }

  function updateDraft<K extends keyof ModelConfigDraft>(key: K, value: ModelConfigDraft[K]) {
    setDraft(current => ({ ...current, [key]: value }));
  }

  async function handleSave() {
    if (!draft.name.trim() || !draft.providerType.trim() || !draft.baseUrl.trim() || !draft.defaultModel.trim()) {
      Toast.warning("请先填写名称、供应商、Base URL 和默认模型。");
      return;
    }
    if (dialogMode === "create" && !draft.apiKey.trim()) {
      Toast.warning("新建模型配置时必须填写 API Key。");
      return;
    }

    setSaving(true);
    try {
      if (dialogMode === "create") {
        await api.createModelConfig(toCreateModelConfigRequest(draft));
        Toast.success("模型配置已创建。");
      } else if (selectedItem) {
        await api.updateModelConfig(selectedItem.id, toUpdateModelConfigRequest(draft));
        Toast.success("模型配置已更新。");
      }
      setModalVisible(false);
      await load();
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "保存模型配置失败。");
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(item: ModelConfigItem) {
    Modal.confirm({
      title: "删除模型配置",
      content: `确定删除 ${item.name} 吗？`,
      onOk: async () => {
        try {
          await api.deleteModelConfig(item.id);
          Toast.success("模型配置已删除。");
          await load();
        } catch (error) {
          Toast.error(error instanceof Error ? error.message : "删除模型配置失败。");
        }
      }
    });
  }

  async function handleConnectionTest() {
    const payload: ModelConfigConnectionTestRequest = {
      modelConfigId: selectedItem?.id,
      providerType: draft.providerType,
      apiKey: draft.apiKey,
      baseUrl: draft.baseUrl,
      model: draft.defaultModel
    };

    setTesting(true);
    try {
      const result = await api.testModelConfigConnection(payload);
      setTestResult(
        result.success
          ? `连通成功，耗时 ${result.latencyMs ?? 0} ms`
          : `连通失败：${result.errorMessage || "未知错误"}`
      );
      Toast.success(result.success ? "模型连通性验证通过。" : "模型连通性验证已返回错误。");
    } catch (error) {
      const message = error instanceof Error ? error.message : "模型连接测试失败。";
      setTestResult(message);
      Toast.error(message);
    } finally {
      setTesting(false);
    }
  }

  async function handlePromptTest() {
    const payload: ModelConfigPromptTestRequest = {
      modelConfigId: selectedItem?.id,
      providerType: draft.providerType,
      apiKey: draft.apiKey,
      baseUrl: draft.baseUrl,
      model: draft.defaultModel,
      prompt,
      enableReasoning: draft.enableReasoning,
      enableTools: draft.enableTools,
      enableStreaming: draft.enableStreaming
    };

    setTesting(true);
    setTestResult("正在执行 Prompt 测试...");
    try {
      const result = await api.runModelConfigPromptTest(payload);
      setTestResult(result || "模型已返回空响应。");
      Toast.success("Prompt 测试完成。");
    } catch (error) {
      const message = error instanceof Error ? error.message : "Prompt 测试失败。";
      setTestResult(message);
      Toast.error(message);
    } finally {
      setTesting(false);
    }
  }

  return (
    <Surface
      title="Model Configs"
      subtitle="模型配置列表与测试工作台"
      testId="app-model-configs-page"
      toolbar={
        <Space>
          <Input
            value={keyword}
            onChange={setKeyword}
            placeholder="搜索供应商 / 模型"
            showClear
            data-testid="app-model-configs-search"
          />
          <Button icon={<IconPlus />} theme="solid" type="primary" onClick={openCreateDialog} data-testid="app-model-configs-create">
            新增模型
          </Button>
        </Space>
      }
    >
      <div className="module-studio__stack">
        {stats ? (
          <div className="module-studio__grid" data-testid="app-model-configs-stats">
            {[
              { label: "总数", value: stats.total, color: "blue" },
              { label: "启用", value: stats.enabled, color: "green" },
              { label: "停用", value: stats.disabled, color: "grey" },
              { label: "Embedding", value: stats.embeddingCount, color: "purple" }
            ].map(item => (
              <article key={item.label} className="module-studio__card module-studio__summary-card">
                <Tag color={item.color as "blue"}>{item.label}</Tag>
                <strong>{item.value}</strong>
              </article>
            ))}
          </div>
        ) : null}

        <CardGrid
          testId="app-model-configs-grid"
          items={items}
          render={(item: ModelConfigItem) => (
            <article key={item.id} className="module-studio__card">
              <div className="module-studio__card-head">
                <strong>{item.name}</strong>
                <Tag color={item.isEnabled ? "green" : "grey"}>{item.isEnabled ? "启用" : "停用"}</Tag>
              </div>
              <p>{item.providerType} / {item.defaultModel}</p>
              <span className="module-studio__meta">{formatModelConfigEndpointSummary(item.baseUrl, locale)}</span>
              <span className="module-studio__meta">创建时间：{formatDate(item.createdAt)}</span>
              <div className="module-studio__actions">
                <Button onClick={() => void openEditDialog(item)} data-testid={`app-model-config-edit-${item.id}`}>编辑</Button>
                <Button theme="light" onClick={() => void openEditDialog(item)}>测试</Button>
                <Button type="danger" theme="borderless" onClick={() => void handleDelete(item)} data-testid={`app-model-config-delete-${item.id}`}>
                  删除
                </Button>
              </div>
            </article>
          )}
        />
      </div>

      <Modal
        title={dialogMode === "create" ? "新增模型配置" : `编辑模型配置 · ${selectedItem?.name || ""}`}
        visible={modalVisible}
        onCancel={closeDialog}
        onOk={() => void handleSave()}
        okText={dialogMode === "create" ? "创建" : "保存"}
        confirmLoading={saving}
        width={920}
      >
        <div className="module-studio__model-dialog">
          <div className="module-studio__stack">
            <div className="module-studio__form-grid">
              <label className="module-studio__field">
                <span>名称</span>
                <Input value={draft.name} onChange={value => updateDraft("name", value)} data-testid="app-model-config-name" />
              </label>
              <label className="module-studio__field">
                <span>供应商</span>
                <Select
                  value={draft.providerType}
                  optionList={MODEL_PROVIDER_OPTIONS}
                  onChange={value => updateDraft("providerType", String(value))}
                />
              </label>
              <label className="module-studio__field">
                <span>Base URL</span>
                <Input value={draft.baseUrl} onChange={value => updateDraft("baseUrl", value)} data-testid="app-model-config-base-url" />
              </label>
              <label className="module-studio__field">
                <span>默认模型</span>
                <Input value={draft.defaultModel} onChange={value => updateDraft("defaultModel", value)} data-testid="app-model-config-default-model" />
              </label>
              <label className="module-studio__field">
                <span>模型标识</span>
                <Input value={draft.modelId} onChange={value => updateDraft("modelId", value)} />
              </label>
              <label className="module-studio__field">
                <span>{dialogMode === "create" ? "API Key" : "API Key（留空表示不更新）"}</span>
                <Input value={draft.apiKey} onChange={value => updateDraft("apiKey", value)} mode="password" data-testid="app-model-config-api-key" />
              </label>
              <label className="module-studio__field module-studio__field--full">
                <span>系统提示词</span>
                <textarea
                  value={draft.systemPrompt}
                  onChange={event => updateDraft("systemPrompt", event.target.value)}
                  rows={4}
                  className="module-studio__textarea"
                />
              </label>
            </div>

            <div className="module-studio__model-switches">
              {[
                ["isEnabled", "启用配置"],
                ["supportsEmbedding", "支持 Embedding"],
                ["enableStreaming", "流式输出"],
                ["enableReasoning", "推理增强"],
                ["enableTools", "工具调用"],
                ["enableVision", "视觉能力"],
                ["enableJsonMode", "JSON 模式"]
              ].map(([key, label]) => (
                <label key={key} className="module-studio__switch-item">
                  <span>{label}</span>
                  <Switch
                    checked={Boolean(draft[key as keyof ModelConfigDraft])}
                    onChange={checked => updateDraft(key as keyof ModelConfigDraft, checked)}
                  />
                </label>
              ))}
            </div>

            <div className="module-studio__number-grid">
              <InputNumber value={draft.temperature} onChange={value => updateDraft("temperature", typeof value === "number" ? value : undefined)} placeholder="Temperature" />
              <InputNumber value={draft.maxTokens} onChange={value => updateDraft("maxTokens", typeof value === "number" ? value : undefined)} placeholder="Max Tokens" />
              <InputNumber value={draft.topP} onChange={value => updateDraft("topP", typeof value === "number" ? value : undefined)} placeholder="Top P" />
              <InputNumber value={draft.frequencyPenalty} onChange={value => updateDraft("frequencyPenalty", typeof value === "number" ? value : undefined)} placeholder="Frequency Penalty" />
              <InputNumber value={draft.presencePenalty} onChange={value => updateDraft("presencePenalty", typeof value === "number" ? value : undefined)} placeholder="Presence Penalty" />
            </div>
          </div>

          <div className="module-studio__ide-sidecard">
            <Typography.Title heading={6}>联调测试</Typography.Title>
            <Typography.Text type="tertiary">
              这里直接连 AppHost 做连接测试和 Prompt 流式测试，不再走假数据。
            </Typography.Text>
            <Space>
              <Button onClick={() => void handleConnectionTest()} loading={testing} data-testid="app-model-config-test-connection">
                测试连接
              </Button>
              <Button theme="solid" onClick={() => void handlePromptTest()} loading={testing} data-testid="app-model-config-test-prompt">
                Prompt 测试
              </Button>
            </Space>
            <textarea
              value={prompt}
              onChange={event => setPrompt(event.target.value)}
              rows={5}
              className="module-studio__textarea"
            />
            <textarea
              value={testResult}
              readOnly
              rows={10}
              className="module-studio__textarea"
              data-testid="app-model-config-test-result"
            />
            {selectedItem ? (
              <ResourceReferenceCard
                api={api}
                locale={locale}
                resourceType="model-config"
                resourceId={String(selectedItem.id)}
                title="引用关系"
                testId="app-model-config-reference-card"
              />
            ) : null}
          </div>
        </div>
      </Modal>
    </Surface>
  );
}

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
import type {
  AgentListItem,
  AgentDetail,
  ChatMessageItem,
  ConversationItem,
  StudioApplicationCreateRequest,
  StudioApplicationSummary,
  StudioWorkspaceOverview,
  DevelopFocus,
  DevelopResourceSummary,
  ModelConfigConnectionTestRequest,
  ModelConfigCreateRequest,
  ModelConfigItem,
  ModelConfigPromptTestRequest,
  ModelConfigStats,
  ModelConfigUpdateRequest,
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

function formatDate(value?: string) {
  if (!value) {
    return "-";
  }
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
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

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function parseJsonSafely(value?: string): unknown {
  if (!value) {
    return null;
  }

  try {
    return JSON.parse(value);
  } catch {
    const decoded = decodeHtmlEntities(value);
    if (!decoded || decoded === value) {
      return null;
    }

    try {
      return JSON.parse(decoded);
    } catch {
      return null;
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
    return isRecord(nested) ? extractSecurityIncidentTaskCard(nested) : null;
  }

  if (!isRecord(initial)) {
    return null;
  }

  if (typeof initial.result === "string") {
    const nested = parseJsonSafely(initial.result);
    if (isRecord(nested)) {
      return extractSecurityIncidentTaskCard(nested);
    }
  }

  return extractSecurityIncidentTaskCard(initial);
}

function extractSecurityIncidentTaskCard(payload: Record<string, unknown>): SecurityIncidentTaskCard | null {
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

function formatModelConfigEndpointSummary(baseUrl?: string): string {
  return baseUrl?.trim() ? "接入地址已配置" : "接入地址未配置";
}

function parseTraceSummary(metadata?: string): WorkbenchTrace | null {
  const payload = parseJsonSafely(metadata);
  if (!isRecord(payload) || !isRecord(payload.trace)) {
    return null;
  }

  const trace = payload.trace;
  const steps = Array.isArray(trace.steps)
    ? trace.steps
        .filter(isRecord)
        .map(step => ({
          nodeKey: typeof step.nodeKey === "string" ? step.nodeKey : "unknown",
          status: typeof step.status === "string" ? step.status : undefined,
          nodeType: typeof step.nodeType === "string" ? step.nodeType : undefined,
          durationMs: typeof step.durationMs === "number" ? step.durationMs : undefined,
          errorMessage: typeof step.errorMessage === "string" ? step.errorMessage : undefined
        }))
    : [];

  return {
    executionId: typeof trace.executionId === "string" ? trace.executionId : "",
    status: typeof trace.status === "string" ? trace.status : undefined,
    startedAt: typeof trace.startedAt === "string" ? trace.startedAt : undefined,
    completedAt: typeof trace.completedAt === "string" ? trace.completedAt : undefined,
    durationMs: typeof trace.durationMs === "number" ? trace.durationMs : undefined,
    steps
  };
}

interface AgentPromptSections {
  persona: string;
  goals: string;
  skills: string;
  workflow: string;
  outputFormat: string;
  constraints: string;
  opening: string;
}

const EMPTY_AGENT_PROMPT_SECTIONS: AgentPromptSections = {
  persona: "",
  goals: "",
  skills: "",
  workflow: "",
  outputFormat: "",
  constraints: "",
  opening: ""
};

const AGENT_PROMPT_SECTION_MAP: Array<{ title: string; key: keyof AgentPromptSections }> = [
  { title: "角色", key: "persona" },
  { title: "目标", key: "goals" },
  { title: "技能", key: "skills" },
  { title: "工作流", key: "workflow" },
  { title: "输出格式", key: "outputFormat" },
  { title: "限制", key: "constraints" },
  { title: "开场白", key: "opening" }
];

function parseAgentPromptSections(systemPrompt?: string): AgentPromptSections {
  if (!systemPrompt?.trim()) {
    return { ...EMPTY_AGENT_PROMPT_SECTIONS };
  }

  const sections = { ...EMPTY_AGENT_PROMPT_SECTIONS };
  let currentKey: keyof AgentPromptSections | null = null;

  for (const line of systemPrompt.split(/\r?\n/)) {
    const matched = AGENT_PROMPT_SECTION_MAP.find(section => line.trim() === `## ${section.title}`);
    if (matched) {
      currentKey = matched.key;
      continue;
    }

    if (!currentKey) {
      continue;
    }

    sections[currentKey] = sections[currentKey]
      ? `${sections[currentKey]}\n${line}`
      : line;
  }

  const hasParsedSection = AGENT_PROMPT_SECTION_MAP.some(section => sections[section.key].trim().length > 0);
  if (!hasParsedSection) {
    return {
      ...EMPTY_AGENT_PROMPT_SECTIONS,
      persona: systemPrompt.trim()
    };
  }

  return sections;
}

function composeAgentPromptSections(sections: AgentPromptSections): string {
  return AGENT_PROMPT_SECTION_MAP
    .map(section => {
      const value = sections[section.key].trim();
      if (!value) {
        return "";
      }

      return `## ${section.title}\n${value}`;
    })
    .filter(Boolean)
    .join("\n\n");
}

export function DevelopPage({
  api,
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
  const [agentDraft, setAgentDraft] = useState({ name: "", description: "" });
  const [applicationDraft, setApplicationDraft] = useState<StudioApplicationCreateRequest>({
    name: "",
    description: "",
    icon: ""
  });
  const [submitting, setSubmitting] = useState(false);

  const load = async () => {
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

  const recentResources = useMemo(() => {
    return [...workspaceResources]
      .sort((left, right) => String(right.lastEditedAt || right.updatedAt || "").localeCompare(String(left.lastEditedAt || left.updatedAt || "")))
      .slice(0, 8);
  }, [workspaceResources]);

  const summaryCards = [
    {
      key: "projects",
      title: "应用",
      description: "把项目资源、组织协作和编排能力收拢到同一个应用工作台里。",
      count: workspaceSummary?.appCount ?? applications.length,
      actionLabel: "查看应用",
      onClick: () => setActiveFocus("projects")
    },
    {
      key: "agents",
      title: "Agent",
      description: "配置智能体、提示词和对话调试。",
      count: workspaceSummary?.agentCount ?? items.length,
      actionLabel: "进入 Agent",
      onClick: () => setActiveFocus("agents")
    },
    {
      key: "workflow",
      title: "Workflow",
      description: "编排自动化流程与测试运行。",
      count: workspaceSummary?.workflowCount ?? workflowItems.length,
      actionLabel: "进入 Workflow",
      onClick: onOpenWorkflows
    },
    {
      key: "chatflow",
      title: "Chatflow",
      description: "面向对话流的编排与发布。",
      count: workspaceSummary?.chatflowCount ?? chatflowItems.length,
      actionLabel: "进入 Chatflow",
      onClick: onOpenChatflows
    },
    {
      key: "models",
      title: "模型配置",
      description: "管理应用端大模型供应商与默认模型。",
      count: models.length,
      actionLabel: "打开模型配置",
      onClick: onOpenModelConfigs
    },
    {
      key: "members",
      title: "组织成员",
      description: "组织架构能力并入 Coze 菜单，开发和治理不再割裂。",
      count: workspaceOverview?.memberCount ?? 0,
      actionLabel: "成员管理",
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
      Toast.warning("请先填写智能体名称。");
      return;
    }

    setSubmitting(true);
    try {
      const botId = await api.createAgent({
        name: agentDraft.name.trim(),
        description: agentDraft.description.trim() || undefined,
        systemPrompt: composeAgentPromptSections({
          ...EMPTY_AGENT_PROMPT_SECTIONS,
          persona: agentDraft.description.trim() || `${agentDraft.name.trim()} 的角色说明`
        }),
        enableMemory: true,
        enableShortTermMemory: true,
        enableLongTermMemory: true,
        longTermMemoryTopK: 3
      });
      setAgentDialogVisible(false);
      Toast.success("智能体已创建。");
      await load();
      onOpenBot(botId);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "创建智能体失败。");
    } finally {
      setSubmitting(false);
    }
  }

  async function handleSaveApplication() {
    if (!applicationDraft.name.trim()) {
      Toast.warning("请先填写应用名称。");
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
        Toast.success("应用信息已更新。");
      } else {
        const created = await api.createApplication({
          name: applicationDraft.name.trim(),
          description: applicationDraft.description?.trim() || undefined,
          icon: applicationDraft.icon?.trim() || undefined
        });
        Toast.success("应用已创建。");
        onOpenWorkflow(created.workflowId);
      }
      setApplicationDialogVisible(false);
      await load();
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "保存应用失败。");
    } finally {
      setSubmitting(false);
    }
  }

  async function handleDeleteApplication(item: StudioApplicationSummary) {
    Modal.confirm({
      title: "删除应用",
      content: `确定删除应用 ${item.name} 吗？`,
      onOk: async () => {
        try {
          await api.deleteApplication(item.id);
          Toast.success("应用已删除。");
          await load();
        } catch (error) {
          Toast.error(error instanceof Error ? error.message : "删除应用失败。");
        }
      }
    });
  }

  async function handleToggleFavorite(resource: WorkspaceIdeResource) {
    try {
      await api.toggleWorkspaceFavorite(resource.resourceType, resource.resourceId, !resource.isFavorite);
      await load();
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "更新收藏状态失败。");
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

  return (
    <Surface
      title="项目开发"
      subtitle="按 Coze 风格把应用、智能体、工作流与组织协作放进同一套开发台。"
      testId="app-develop-page"
      toolbar={
        <Dropdown
          position="bottomRight"
          render={
            <div className="module-studio__coze-menu">
              <button type="button" className="module-studio__coze-menu-item" onClick={openCreateApplicationDialog}>新建应用</button>
              <button type="button" className="module-studio__coze-menu-item" onClick={openCreateAgentDialog}>新建智能体</button>
              <button type="button" className="module-studio__coze-menu-item" onClick={onCreateWorkflow}>新建工作流</button>
              <button type="button" className="module-studio__coze-menu-item" onClick={onCreateChatflow}>新建对话流</button>
            </div>
          }
        >
          <Button icon={<IconPlus />} theme="solid" type="primary" data-testid="app-develop-create-menu">
            创建
          </Button>
        </Dropdown>
      }
    >
      <div className="module-studio__coze-page">
        <section className="module-studio__coze-hero">
          <div className="module-studio__coze-hero-copy">
            <span className="module-studio__coze-kicker">Workspace IDE</span>
            <Typography.Title heading={2} style={{ margin: 0 }}>用 Coze 风格工作空间组织应用、智能体和团队协作</Typography.Title>
            <Typography.Text type="tertiary">
              从这里直接进入应用卡片、智能体编排、工作流资源、模型配置和组织治理，不需要在多个后台之间来回切换。
            </Typography.Text>
          </div>
          <div className="module-studio__coze-hero-actions">
            <Button theme="solid" type="primary" onClick={onOpenAgentChat}>预览与调试</Button>
            <Button onClick={onOpenLibrary}>资源库</Button>
            <Button onClick={onOpenModelConfigs}>模型配置</Button>
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
            placeholder="搜索应用、智能体、工作流、对话流"
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
              <Radio value="overview">总览</Radio>
              <Radio value="projects">应用</Radio>
              <Radio value="agents">Agent</Radio>
              <Radio value="workflow">Workflow</Radio>
              <Radio value="chatflow">Chatflow</Radio>
              <Radio value="plugins">插件</Radio>
              <Radio value="data">数据</Radio>
              <Radio value="models">模型</Radio>
              <Radio value="chat">最近编辑</Radio>
            </Radio.Group>
            <Switch
              checked={favoriteOnly}
              onChange={checked => setFavoriteOnly(checked)}
              checkedText="仅收藏"
              uncheckedText="全部资源"
            />
          </Space>
        </div>

        <div className="module-studio__coze-board">
          <div className="module-studio__coze-board-main">
            {(activeFocus === "overview" || activeFocus === "projects") ? (
              <section className="module-studio__coze-section">
                <div className="module-studio__section-head">
                  <div>
                    <Typography.Title heading={5} style={{ margin: 0 }}>应用</Typography.Title>
                    <Typography.Text type="tertiary">应用卡片承载项目级组织、资源入口和业务编排上下文。</Typography.Text>
                  </div>
                  <Space>
                    <Button onClick={openCreateApplicationDialog}>新建应用</Button>
                    <Button theme="borderless" onClick={() => setActiveFocus("projects")}>查看全部</Button>
                  </Space>
                </div>
                <CardGrid
                  testId="app-develop-projects-grid"
                  items={filteredApplications}
                  render={(item: StudioApplicationSummary) => (
                    <article key={item.id} className={`module-studio__coze-card${selectedApplication?.id === item.id ? " is-active" : ""}`}>
                      <div className="module-studio__card-head">
                        <div>
                          <Tag color="light-blue">应用</Tag>
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
                      <p>{item.description || "AI 应用将智能体、工作流和提示模板组织成统一交付入口。"}</p>
                      <span className="module-studio__meta">最近更新：{formatDate(item.lastEditedAt || item.updatedAt)}</span>
                      <div className="module-studio__actions">
                        <Button onClick={() => setSelectedApplicationId(item.id)}>查看详情</Button>
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
                          打开 IDE
                        </Button>
                        <Button theme="borderless" onClick={() => openEditApplicationDialog(item)}>编辑</Button>
                        <Button theme="borderless" type="danger" onClick={() => void handleDeleteApplication(item)}>删除</Button>
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
                    <Typography.Title heading={5} style={{ margin: 0 }}>智能体</Typography.Title>
                    <Typography.Text type="tertiary">把模型、记忆和工作流绑定在智能体上，进入专属 IDE 做调试和发布。</Typography.Text>
                  </div>
                  <Button theme="borderless" onClick={() => setActiveFocus("agents")}>查看全部</Button>
                </div>
                <CardGrid
                  testId="app-develop-agents-grid"
                  items={filteredAgents}
                  render={(item: AgentListItem) => (
                    <article key={item.id} className="module-studio__coze-card">
                      <div className="module-studio__card-head">
                        <div>
                          <Tag color="cyan">智能体</Tag>
                          <strong>{item.name}</strong>
                        </div>
                        <Tag color="blue">{item.status}</Tag>
                      </div>
                      <p>{item.description || item.modelName || "暂无描述"}</p>
                      <span className="module-studio__meta">创建时间：{formatDate(item.createdAt)}</span>
                      <Button onClick={() => onOpenBot(item.id)}>打开 IDE</Button>
                    </article>
                  )}
                />
              </section>
            ) : null}

            {(activeFocus === "overview" || activeFocus === "workflow") ? (
              <section className="module-studio__coze-section">
                <div className="module-studio__section-head">
                  <div>
                    <Typography.Title heading={5} style={{ margin: 0 }}>工作流</Typography.Title>
                    <Typography.Text type="tertiary">从业务编排、数据库处理到知识库检索，把可执行逻辑沉淀成标准工作流资产。</Typography.Text>
                  </div>
                  <Space>
                    <Button onClick={onCreateWorkflow}>新建工作流</Button>
                    <Button theme="borderless" onClick={onOpenWorkflows}>资源列表</Button>
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
                      <p>{item.description || item.meta || "暂无描述"}</p>
                      <span className="module-studio__meta">最近更新：{formatDate(item.updatedAt)}</span>
                      <Button onClick={() => onOpenWorkflow(item.id)}>打开编辑器</Button>
                    </article>
                  )}
                />
              </section>
            ) : null}

            {(activeFocus === "overview" || activeFocus === "chatflow") ? (
              <section className="module-studio__coze-section">
                <div className="module-studio__section-head">
                  <div>
                    <Typography.Title heading={5} style={{ margin: 0 }}>对话流</Typography.Title>
                    <Typography.Text type="tertiary">适合多轮交互、用户引导和 Agent 场景，把会话意图转成流程节点。</Typography.Text>
                  </div>
                  <Space>
                    <Button onClick={onCreateChatflow}>新建对话流</Button>
                    <Button theme="borderless" onClick={onOpenChatflows}>资源列表</Button>
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
                      <p>{item.description || item.meta || "暂无描述"}</p>
                      <span className="module-studio__meta">最近更新：{formatDate(item.updatedAt)}</span>
                      <Button onClick={() => onOpenChatflow(item.id)}>打开编辑器</Button>
                    </article>
                  )}
                />
              </section>
            ) : null}

            {(activeFocus === "overview" || activeFocus === "plugins") ? (
              <section className="module-studio__coze-section">
                <div className="module-studio__section-head">
                  <div>
                    <Typography.Title heading={5} style={{ margin: 0 }}>插件</Typography.Title>
                    <Typography.Text type="tertiary">把 Bot 技能、HTTP/OpenAPI 能力与工具调用统一沉淀在 Coze 工作空间里。</Typography.Text>
                  </div>
                  <Button theme="borderless" onClick={onOpenLibrary}>进入资源库</Button>
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
                      <p>{item.description || "插件工具能力"}</p>
                      <span className="module-studio__meta">最近更新：{formatDate(item.lastEditedAt || item.updatedAt)}</span>
                      <Button onClick={onOpenLibrary}>打开资源库</Button>
                    </article>
                  )}
                />
              </section>
            ) : null}

            {(activeFocus === "overview" || activeFocus === "data") ? (
              <section className="module-studio__coze-section">
                <div className="module-studio__section-head">
                  <div>
                    <Typography.Title heading={5} style={{ margin: 0 }}>数据与知识</Typography.Title>
                    <Typography.Text type="tertiary">知识库、数据库和变量继续沉淀在统一工作空间里，为智能体与工作流复用。</Typography.Text>
                  </div>
                  <Button theme="borderless" onClick={onOpenLibrary}>统一管理</Button>
                </div>
                <CardGrid
                  testId="app-develop-data-grid"
                  items={dataResources}
                  render={(item: WorkspaceIdeResource) => (
                    <article key={`${item.resourceType}-${item.resourceId}`} className="module-studio__coze-card">
                      <div className="module-studio__card-head">
                        <div>
                          <Tag color={item.resourceType === "knowledge-base" ? "green" : "blue"}>
                            {item.resourceType === "knowledge-base" ? "知识库" : "数据库"}
                          </Tag>
                          <strong>{item.name}</strong>
                        </div>
                        <Button theme="borderless" onClick={() => void handleToggleFavorite(item)}>{item.isFavorite ? "★" : "☆"}</Button>
                      </div>
                      <p>{item.description || "可被工作流与智能体直接复用的数据资源。"}</p>
                      <span className="module-studio__meta">最近更新：{formatDate(item.lastEditedAt || item.updatedAt)}</span>
                      <Button onClick={onOpenLibrary}>打开资源库</Button>
                    </article>
                  )}
                />
              </section>
            ) : null}

            {(activeFocus === "overview" || activeFocus === "models") ? (
              <section className="module-studio__coze-section">
                <div className="module-studio__section-head">
                  <div>
                    <Typography.Title heading={5} style={{ margin: 0 }}>模型配置</Typography.Title>
                    <Typography.Text type="tertiary">统一管理模型供应商、推理能力、工具调用和提示词测试。</Typography.Text>
                  </div>
                  <Button theme="borderless" onClick={onOpenModelConfigs}>管理模型</Button>
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
                        <Tag color={item.isEnabled ? "green" : "grey"}>{item.isEnabled ? "启用" : "停用"}</Tag>
                      </div>
                      <p>{item.providerType} / {item.defaultModel}</p>
                      <span className="module-studio__meta">创建时间：{formatDate(item.createdAt)}</span>
                    </article>
                  )}
                />
              </section>
            ) : null}

            {(activeFocus === "overview" || activeFocus === "chat") ? (
              <section className="module-studio__coze-section">
                <div className="module-studio__section-head">
                  <div>
                    <Typography.Title heading={5} style={{ margin: 0 }}>最近编辑</Typography.Title>
                    <Typography.Text type="tertiary">回到最近访问过的资源，继续编辑、调试或发布。</Typography.Text>
                  </div>
                  <Button theme="borderless" onClick={onOpenAgentChat}>进入对话调试</Button>
                </div>
                <CardGrid
                  testId="app-develop-recent-grid"
                  items={recentResources}
                  render={(item: WorkspaceIdeResource) => (
                    <article key={`${item.resourceType}-${item.resourceId}`} className="module-studio__coze-card module-studio__coze-card--compact">
                      <Tag color={item.resourceType === "chatflow" ? "purple" : item.resourceType === "workflow" ? "blue" : item.resourceType === "agent" ? "cyan" : "green"}>
                        {item.resourceType}
                      </Tag>
                      <strong>{item.name}</strong>
                      <p>{item.description || item.badge || item.status || "最近访问资源"}</p>
                      <span className="module-studio__meta">最近时间：{formatDate(item.lastEditedAt || item.updatedAt)}</span>
                      {item.resourceType === "agent" ? <Button onClick={() => onOpenBot(item.resourceId)}>继续编辑</Button> : null}
                      {item.resourceType === "workflow" ? <Button onClick={() => onOpenWorkflow(item.resourceId)}>继续编辑</Button> : null}
                      {item.resourceType === "chatflow" ? <Button onClick={() => onOpenChatflow(item.resourceId)}>继续编辑</Button> : null}
                      {item.resourceType === "app" && item.linkedWorkflowId ? <Button onClick={() => onOpenWorkflow(item.linkedWorkflowId!)}>继续编辑</Button> : null}
                      {(item.resourceType === "plugin" || item.resourceType === "knowledge-base" || item.resourceType === "database") ? <Button onClick={onOpenLibrary}>打开资源库</Button> : null}
                    </article>
                  )}
                />
              </section>
            ) : null}
          </div>

          <aside className="module-studio__coze-board-side">
            <section className="module-studio__coze-sidepanel">
              <Typography.Title heading={6}>应用详情</Typography.Title>
              {selectedApplication ? (
                <div className="module-studio__stack">
                  <Tag color={selectedApplication.status?.toLowerCase() === "published" ? "green" : "blue"}>
                    {selectedApplication.status || "Draft"}
                  </Tag>
                  <strong>{selectedApplication.name}</strong>
                  <Typography.Text type="tertiary">{selectedApplication.description || "当前应用还没有补充描述。"}</Typography.Text>
                  <span className="module-studio__meta">最近更新：{formatDate(selectedApplication.lastEditedAt || selectedApplication.updatedAt)}</span>
                  <Space wrap>
                    <Button
                      theme="solid"
                      type="primary"
                      onClick={() => {
                        if (selectedApplication.workflowId) {
                          onOpenWorkflow(selectedApplication.workflowId);
                        }
                      }}
                    >
                      进入工作流 IDE
                    </Button>
                    <Button onClick={() => openEditApplicationDialog(selectedApplication)}>编辑应用</Button>
                  </Space>
                </div>
              ) : (
                <Empty title="暂无应用" image={null} />
              )}
            </section>

            <section className="module-studio__coze-sidepanel">
              <Typography.Title heading={6}>组织架构</Typography.Title>
              <div className="module-studio__coze-linklist">
                <button type="button" className="module-studio__coze-linkrow" onClick={onOpenUsers}>
                  <span>成员管理</span>
                  <strong>{workspaceOverview?.memberCount ?? 0}</strong>
                </button>
                <button type="button" className="module-studio__coze-linkrow" onClick={onOpenRoles}>
                  <span>角色管理</span>
                  <strong>{workspaceOverview?.roleCount ?? 0}</strong>
                </button>
                <button type="button" className="module-studio__coze-linkrow" onClick={onOpenDepartments}>
                  <span>部门管理</span>
                  <strong>{workspaceOverview?.departmentCount ?? 0}</strong>
                </button>
                <button type="button" className="module-studio__coze-linkrow" onClick={onOpenPositions}>
                  <span>岗位管理</span>
                  <strong>{workspaceOverview?.positionCount ?? 0}</strong>
                </button>
              </div>
              {(workspaceOverview?.uncoveredMemberCount ?? 0) > 0 ? (
                <Banner
                  type="warning"
                  bordered={false}
                  fullMode={false}
                  title="组织治理提醒"
                  description={`当前仍有 ${workspaceOverview?.uncoveredMemberCount ?? 0} 个成员未被角色权限覆盖。`}
                />
              ) : null}
            </section>

            <section className="module-studio__coze-sidepanel">
              <Typography.Title heading={6}>快速入口</Typography.Title>
              <div className="module-studio__coze-linklist">
                <button type="button" className="module-studio__coze-linkrow" onClick={onOpenLibrary}>
                  <span>资源库</span>
                  <strong>Open</strong>
                </button>
                <button type="button" className="module-studio__coze-linkrow" onClick={onOpenAgentChat}>
                  <span>预览与调试</span>
                  <strong>Chat</strong>
                </button>
                <button type="button" className="module-studio__coze-linkrow" onClick={onOpenModelConfigs}>
                  <span>模型配置</span>
                  <strong>{models.length}</strong>
                </button>
                <button type="button" className="module-studio__coze-linkrow" onClick={() => setFavoriteOnly(current => !current)}>
                  <span>收藏资源</span>
                  <strong>{workspaceSummary?.favoriteCount ?? 0}</strong>
                </button>
                <button type="button" className="module-studio__coze-linkrow" onClick={() => setActiveFocus("chat")}>
                  <span>最近编辑</span>
                  <strong>{workspaceSummary?.recentCount ?? 0}</strong>
                </button>
              </div>
            </section>
          </aside>
        </div>

        <Modal
          title="新建智能体"
          visible={agentDialogVisible}
          onCancel={() => {
            if (!submitting) {
              setAgentDialogVisible(false);
            }
          }}
          onOk={() => void handleCreateAgent()}
          okText="创建"
          cancelText="取消"
          confirmLoading={submitting}
        >
          <div className="module-studio__stack">
            <Input value={agentDraft.name} onChange={value => setAgentDraft(current => ({ ...current, name: value }))} placeholder="智能体名称" />
            <Input value={agentDraft.description} onChange={value => setAgentDraft(current => ({ ...current, description: value }))} placeholder="角色描述" />
          </div>
        </Modal>

        <Modal
          title={applicationEditing ? "编辑应用" : "新建应用"}
          visible={applicationDialogVisible}
          onCancel={() => {
            if (!submitting) {
              setApplicationDialogVisible(false);
            }
          }}
          onOk={() => void handleSaveApplication()}
          okText={applicationEditing ? "保存" : "创建"}
          cancelText="取消"
          confirmLoading={submitting}
        >
          <div className="module-studio__stack">
            <Input value={applicationDraft.name} onChange={value => setApplicationDraft(current => ({ ...current, name: value }))} placeholder="应用名称" />
            <Input
              value={applicationDraft.description ?? ""}
              onChange={value => setApplicationDraft(current => ({ ...current, description: value }))}
              placeholder="应用描述"
            />
            <Input
              value={applicationDraft.icon ?? ""}
              onChange={value => setApplicationDraft(current => ({ ...current, icon: value }))}
              placeholder="图标标识（可选）"
            />
          </div>
        </Modal>
      </div>
    </Surface>
  );
}

export function BotIdePage({ api, botId }: StudioPageProps & { botId: string }) {
  const [detail, setDetail] = useState<AgentDetail | null>(null);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [avatarUrl, setAvatarUrl] = useState("");
  const [openingMessage, setOpeningMessage] = useState("");
  const [presetQuestionsInput, setPresetQuestionsInput] = useState("");
  const [systemPrompt, setSystemPrompt] = useState("");
  const [promptSections, setPromptSections] = useState<AgentPromptSections>({ ...EMPTY_AGENT_PROMPT_SECTIONS });
  const [modelConfigId, setModelConfigId] = useState<string | undefined>(undefined);
  const [workflowId, setWorkflowId] = useState<string | undefined>(undefined);
  const [enableMemory, setEnableMemory] = useState(true);
  const [enableShortTermMemory, setEnableShortTermMemory] = useState(true);
  const [enableLongTermMemory, setEnableLongTermMemory] = useState(true);
  const [longTermMemoryTopK, setLongTermMemoryTopK] = useState<number | undefined>(3);
  const [modelConfigs, setModelConfigs] = useState<ModelConfigItem[]>([]);
  const [workflowOptions, setWorkflowOptions] = useState<WorkflowListItem[]>([]);
  const [conversationId, setConversationId] = useState<string>("");
  const [messages, setMessages] = useState<ChatMessageItem[]>([]);
  const [messageInput, setMessageInput] = useState("");
  const [workflowInput, setWorkflowInput] = useState("");
  const [sending, setSending] = useState(false);
  const [saving, setSaving] = useState(false);
  const [runningWorkflow, setRunningWorkflow] = useState(false);
  const [streamingAssistant, setStreamingAssistant] = useState("");
  const [thoughts, setThoughts] = useState<string[]>([]);
  const [lastTrace, setLastTrace] = useState<WorkbenchTrace | null>(null);
  const [workbenchLoading, setWorkbenchLoading] = useState(true);
  const [workbenchError, setWorkbenchError] = useState<string | null>(null);
  const workbenchRequestIdRef = useRef(0);
  const conversationIdRef = useRef("");
  const ensureConversationPromiseRef = useRef<Promise<string> | null>(null);

  function applyConversationId(nextConversationId?: string | null) {
    const normalized = nextConversationId?.trim() ?? "";
    conversationIdRef.current = normalized;
    setConversationId(normalized);
    return normalized;
  }

  async function loadWorkbench(nextConversationId?: string) {
    const requestId = ++workbenchRequestIdRef.current;
    setWorkbenchLoading(true);
    setWorkbenchError(null);

    try {
      const [nextDetail, modelResult, workflows] = await Promise.all([
        api.getAgent(botId),
        api.listModelConfigs(),
        api.listWorkflows({ status: "all" })
      ]);

      if (requestId !== workbenchRequestIdRef.current) {
        return;
      }

      setDetail(nextDetail);
      setName(nextDetail.name);
      setDescription(nextDetail.description || "");
      setAvatarUrl(nextDetail.avatarUrl || "");
      setOpeningMessage(nextDetail.openingMessage || "");
      setPresetQuestionsInput((nextDetail.presetQuestions ?? []).join("\n"));
      setSystemPrompt(nextDetail.systemPrompt || "");
      const parsedPromptSections = parseAgentPromptSections(nextDetail.systemPrompt || "");
      setPromptSections({
        persona: nextDetail.personaMarkdown || parsedPromptSections.persona,
        goals: nextDetail.goals || parsedPromptSections.goals,
        skills: nextDetail.replyLogic || parsedPromptSections.skills,
        workflow: parsedPromptSections.workflow,
        outputFormat: nextDetail.outputFormat || parsedPromptSections.outputFormat,
        constraints: nextDetail.constraints || parsedPromptSections.constraints,
        opening: nextDetail.openingMessage || parsedPromptSections.opening
      });
      setModelConfigId(nextDetail.modelConfigId);
      setWorkflowId(nextDetail.defaultWorkflowId);
      setEnableMemory(nextDetail.enableMemory ?? true);
      setEnableShortTermMemory(nextDetail.enableShortTermMemory ?? true);
      setEnableLongTermMemory(nextDetail.enableLongTermMemory ?? true);
      setLongTermMemoryTopK(nextDetail.longTermMemoryTopK ?? 3);
      setModelConfigs(
        [...modelResult.items].sort((left, right) =>
          String(right.createdAt ?? "").localeCompare(String(left.createdAt ?? ""))
        )
      );
      setWorkflowOptions(
        [...workflows].sort((left, right) =>
          String(right.updatedAt ?? "").localeCompare(String(left.updatedAt ?? ""))
        )
      );

      const conversationResult = await api.listConversations(botId);
      if (requestId !== workbenchRequestIdRef.current) {
        return;
      }

      const activeConversationId = applyConversationId(
        nextConversationId || conversationIdRef.current || conversationResult.items[0]?.id || ""
      );

      if (!activeConversationId) {
        setMessages([]);
        setLastTrace(null);
        return;
      }

      const nextMessages = await api.getMessages(activeConversationId);
      if (requestId !== workbenchRequestIdRef.current) {
        return;
      }

      setMessages(nextMessages);

      const traceMessage = [...nextMessages].reverse().find(message => parseTraceSummary(message.metadata));
      setLastTrace(traceMessage ? parseTraceSummary(traceMessage.metadata) : null);
    } catch (error) {
      if (requestId !== workbenchRequestIdRef.current) {
        return;
      }

      const message = error instanceof Error ? error.message : "加载 Agent 工作台失败。";
      setWorkbenchError(message);
      setMessages([]);
      setLastTrace(null);
    } finally {
      if (requestId === workbenchRequestIdRef.current) {
        setWorkbenchLoading(false);
      }
    }
  }

  useEffect(() => {
    void loadWorkbench();
  }, [api, botId]);

  const selectedModel = useMemo(
    () => modelConfigs.find(item => String(item.id) === String(modelConfigId ?? "")),
    [modelConfigId, modelConfigs]
  );
  const selectedWorkflow = useMemo(
    () => workflowOptions.find(item => item.id === workflowId),
    [workflowId, workflowOptions]
  );
  const canChat = Boolean(selectedModel && selectedModel.isEnabled);
  const resourceReady = !workbenchLoading && !workbenchError;

  async function ensureConversation(): Promise<string> {
    if (conversationIdRef.current) {
      return conversationIdRef.current;
    }

    if (ensureConversationPromiseRef.current) {
      return ensureConversationPromiseRef.current;
    }

    ensureConversationPromiseRef.current = api
      .createConversation(botId, `${name || "Agent"} 调试会话`)
      .then(createdConversationId => applyConversationId(createdConversationId))
      .finally(() => {
        ensureConversationPromiseRef.current = null;
      });

    return ensureConversationPromiseRef.current;
  }

  async function handleSave() {
    if (!name.trim()) {
      Toast.warning("请先填写 Agent 名称。");
      return;
    }

    const nextSystemPrompt = composeAgentPromptSections(promptSections);
    const presetQuestions = presetQuestionsInput
      .split(/\r?\n/)
      .map(item => item.trim())
      .filter(Boolean)
      .slice(0, 6);
    setSaving(true);
    try {
      await api.updateAgent(botId, {
        name: name.trim(),
        description: description.trim() || undefined,
        avatarUrl: avatarUrl.trim() || undefined,
        systemPrompt: nextSystemPrompt || undefined,
        personaMarkdown: promptSections.persona.trim() || undefined,
        goals: promptSections.goals.trim() || undefined,
        replyLogic: promptSections.skills.trim() || undefined,
        outputFormat: promptSections.outputFormat.trim() || undefined,
        constraints: promptSections.constraints.trim() || undefined,
        openingMessage: openingMessage.trim() || promptSections.opening.trim() || undefined,
        presetQuestions: presetQuestions.length > 0 ? presetQuestions : undefined,
        modelConfigId,
        modelName: selectedModel?.defaultModel,
        defaultWorkflowId: workflowId,
        defaultWorkflowName: selectedWorkflow?.name,
        enableMemory,
        enableShortTermMemory,
        enableLongTermMemory,
        longTermMemoryTopK
      });
      setDetail(current => current ? {
        ...current,
        name: name.trim(),
        description: description.trim() || undefined,
        avatarUrl: avatarUrl.trim() || undefined,
        systemPrompt: nextSystemPrompt || undefined,
        personaMarkdown: promptSections.persona.trim() || undefined,
        goals: promptSections.goals.trim() || undefined,
        replyLogic: promptSections.skills.trim() || undefined,
        outputFormat: promptSections.outputFormat.trim() || undefined,
        constraints: promptSections.constraints.trim() || undefined,
        openingMessage: openingMessage.trim() || promptSections.opening.trim() || undefined,
        presetQuestions: presetQuestions.length > 0 ? presetQuestions : undefined,
        modelConfigId,
        modelName: selectedModel?.defaultModel,
        defaultWorkflowId: workflowId,
        defaultWorkflowName: selectedWorkflow?.name,
        enableMemory,
        enableShortTermMemory,
        enableLongTermMemory,
        longTermMemoryTopK
      } : current);
      setSystemPrompt(nextSystemPrompt);
      await loadWorkbench(conversationIdRef.current || undefined);
      Toast.success("Agent 配置已保存。");
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "保存 Agent 配置失败。");
    } finally {
      setSaving(false);
    }
  }

  async function handleClearConversationContext() {
    if (!conversationId) {
      Toast.warning("当前还没有可清理的会话。");
      return;
    }

    try {
      await api.clearConversationContext(conversationId);
      await loadWorkbench(conversationId);
      Toast.success("会话上下文已清空。");
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "清空上下文失败。");
    }
  }

  async function handleClearConversationHistory() {
    if (!conversationId) {
      Toast.warning("当前还没有可清理的会话。");
      return;
    }

    try {
      await api.clearConversationHistory(conversationId);
      await loadWorkbench(conversationId);
      Toast.success("会话历史已清空。");
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "清空历史失败。");
    }
  }

  async function handleDeleteConversation() {
    if (!conversationId) {
      Toast.warning("当前还没有可删除的会话。");
      return;
    }

    try {
      await api.deleteConversation(conversationId);
      applyConversationId("");
      await loadWorkbench();
      Toast.success("调试会话已删除。");
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "删除会话失败。");
    }
  }

  async function handleBindWorkflow() {
    if (!workflowId) {
      Toast.warning("请先选择一个工作流。");
      return;
    }

    try {
      const binding = await api.bindAgentWorkflow(botId, workflowId);
      setWorkflowId(binding.workflowId || workflowId);
      await loadWorkbench(conversationIdRef.current || undefined);
      Toast.success(`已绑定工作流：${binding.workflowName || selectedWorkflow?.name || workflowId}`);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "绑定工作流失败。");
    }
  }

  async function handleSendMessage() {
    if (!canChat) {
      Toast.warning("请先绑定并启用可用模型。");
      return;
    }

    if (!messageInput.trim()) {
      Toast.warning("请先输入消息。");
      return;
    }

    setSending(true);
    setThoughts([]);
    setStreamingAssistant("");

    try {
      const currentConversationId = await ensureConversation();
      let finalContent = "";
      for await (const chunk of api.sendAgentMessage(botId, {
        conversationId: currentConversationId,
        message: messageInput.trim(),
        enableRag: true
      })) {
        if (chunk.type === "thought") {
          setThoughts(current => [...current, chunk.content]);
          continue;
        }

        finalContent += chunk.content;
        setStreamingAssistant(finalContent);
      }

      setMessageInput("");
      setWorkflowInput(current => current || finalContent || messageInput.trim());
      await loadWorkbench(currentConversationId);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "发送消息失败。");
    } finally {
      setStreamingAssistant("");
      setSending(false);
    }
  }

  async function handleRunWorkflow() {
    if (!workflowId) {
      Toast.warning("请先绑定默认工作流。");
      return;
    }

    const incident = workflowInput.trim() || messageInput.trim();
    if (!incident) {
      Toast.warning("请先输入安全事件描述。");
      return;
    }

    setRunningWorkflow(true);
    try {
      const currentConversationId = await ensureConversation();
      const result = await api.runWorkflowTask(workflowId, incident);
      const card = toSecurityIncidentTaskCard(result.execution.outputsJson);
      const content = card
        ? formatWorkflowResultMessage(card)
        : (result.execution.outputsJson || "工作流已完成，但未返回结构化结果。");
      const metadata = JSON.stringify({
        kind: "workflow-task",
        workflowId,
        workflowName: selectedWorkflow?.name,
        trace: result.trace,
        execution: result.execution
      });

      await api.appendConversationMessage(currentConversationId, {
        role: "tool",
        content,
        metadata
      });

      setLastTrace(result.trace ?? null);
      await loadWorkbench(currentConversationId);
      Toast.success("安全事件处置任务已完成。");
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "执行工作流任务失败。");
    } finally {
      setRunningWorkflow(false);
    }
  }

  function updatePromptSection(key: keyof AgentPromptSections, value: string) {
    setPromptSections(current => ({ ...current, [key]: value }));
  }

  return (
    <Surface title="智能体编排" subtitle="角色设定、记忆开关、工作流绑定和实时调试集中在一个 Coze 风格界面里。" testId="app-bot-ide-page">
      {workbenchLoading ? (
        <Banner type="info" bordered={false} fullMode={false} title="正在加载工作台资源" description="正在同步模型配置、工作流列表和最近会话，请稍候。" />
      ) : null}
      {workbenchError ? (
        <Banner type="danger" bordered={false} fullMode={false} title="工作台资源加载失败" description={workbenchError} />
      ) : null}
      {!canChat ? (
        <Banner
          type="warning"
          bordered={false}
          fullMode={false}
          title="当前智能体还不能发送消息"
          description="请先绑定一个已启用的模型配置；如果模型尚未通过测试，请先到模型配置页完成连接测试和 Prompt 测试。"
        />
      ) : null}
      <div className="module-studio__coze-agent-layout">
        <section className="module-studio__coze-agent-panel">
          <div className="module-studio__section-head">
            <div>
              <Typography.Title heading={5} style={{ margin: 0 }}>人设与回复逻辑</Typography.Title>
              <Typography.Text type="tertiary">按照 Coze 的角色编排方式，把智能体的职责、技能、输出格式和限制拆分成清晰段落。</Typography.Text>
            </div>
            <Tag color="cyan">{detail?.status || "draft"}</Tag>
          </div>
          <div className="module-studio__stack">
            <Input value={name} onChange={setName} placeholder="角色名称" />
            <Input value={description} onChange={setDescription} placeholder="角色概述" />
            <Input value={avatarUrl} onChange={setAvatarUrl} placeholder="头像地址（可选）" data-testid="app-bot-ide-avatar-url" />
            <div className="module-studio__field">
              <span>开场白</span>
              <textarea
                value={openingMessage}
                onChange={event => {
                  setOpeningMessage(event.target.value);
                  updatePromptSection("opening", event.target.value);
                }}
                rows={3}
                className="module-studio__textarea"
                disabled={saving}
                data-testid="app-bot-ide-opening-message"
              />
            </div>
            <div className="module-studio__field">
              <span>预置问题（每行一条，最多 6 条）</span>
              <textarea
                value={presetQuestionsInput}
                onChange={event => setPresetQuestionsInput(event.target.value)}
                rows={4}
                className="module-studio__textarea"
                disabled={saving}
                data-testid="app-bot-ide-preset-questions"
              />
            </div>
            {AGENT_PROMPT_SECTION_MAP.map(section => (
              <div key={section.key} className="module-studio__field">
                <span>{section.title}</span>
                <textarea
                  value={promptSections[section.key]}
                  onChange={event => updatePromptSection(section.key, event.target.value)}
                  rows={section.key === "persona" ? 5 : 4}
                  className="module-studio__textarea"
                  disabled={saving}
                />
              </div>
            ))}
            <Space wrap>
              <Button theme="solid" type="primary" onClick={() => void handleSave()} loading={saving} disabled={!resourceReady} data-testid="app-bot-ide-save">
                保存配置
              </Button>
              <Button onClick={() => void handleBindWorkflow()} disabled={!resourceReady || !workflowId} data-testid="app-bot-ide-bind-workflow">
                绑定工作流
              </Button>
            </Space>
          </div>
        </section>

        <section className="module-studio__coze-agent-panel">
          <div className="module-studio__section-head">
            <div>
              <Typography.Title heading={5} style={{ margin: 0 }}>技能与记忆</Typography.Title>
              <Typography.Text type="tertiary">把模型、工作流和记忆策略组合成可执行智能体能力。</Typography.Text>
            </div>
            <div className="module-studio__meta" data-testid="app-bot-ide-resource-status">
              {workbenchLoading ? "资源加载中" : `模型 ${modelConfigs.length} 个 / 工作流 ${workflowOptions.length} 个`}
            </div>
          </div>

          <div className="module-studio__coze-inspector-list">
            <div className="module-studio__coze-inspector-card">
              <span>模型</span>
              <Select
                value={modelConfigId}
                placeholder="选择模型配置"
                optionList={modelConfigs.map(item => ({
                  label: `${item.name} / ${item.defaultModel}`,
                  value: String(item.id)
                }))}
                onChange={value => setModelConfigId(typeof value === "string" ? value : undefined)}
                disabled={!resourceReady || saving}
                data-testid="app-bot-ide-model-config"
              />
            </div>

            <div className="module-studio__coze-inspector-card">
              <span>工作流</span>
              <Select
                value={workflowId}
                placeholder="绑定默认工作流"
                optionList={workflowOptions.map(item => ({
                  label: `${item.name}${item.status === 1 ? " / 已发布" : " / 草稿"}`,
                  value: item.id
                }))}
                onChange={value => setWorkflowId(typeof value === "string" ? value : undefined)}
                disabled={!resourceReady || saving}
                data-testid="app-bot-ide-workflow-select"
              />
            </div>

            <div className="module-studio__coze-inspector-card">
              <span>记忆开关</span>
              <div className="module-studio__model-switches">
                <div className="module-studio__switch-item">
                  <span>启用记忆</span>
                  <Switch checked={enableMemory} onChange={setEnableMemory} />
                </div>
                <div className="module-studio__switch-item">
                  <span>短期记忆</span>
                  <Switch checked={enableShortTermMemory} onChange={setEnableShortTermMemory} disabled={!enableMemory} />
                </div>
                <div className="module-studio__switch-item">
                  <span>长期记忆</span>
                  <Switch checked={enableLongTermMemory} onChange={setEnableLongTermMemory} disabled={!enableMemory} />
                </div>
              </div>
            </div>

            <div className="module-studio__coze-inspector-card">
              <span>长期记忆召回数量</span>
              <InputNumber value={longTermMemoryTopK} min={1} max={20} onNumberChange={value => setLongTermMemoryTopK(value ?? 3)} disabled={!enableMemory || !enableLongTermMemory} />
            </div>

            <div className="module-studio__coze-inspector-card">
              <span>当前绑定</span>
              <Descriptions
                data={[
                  { key: "agent", value: detail?.name || "-" },
                  { key: "model", value: selectedModel ? `${selectedModel.providerType} / ${selectedModel.defaultModel}` : "尚未绑定模型" },
                  { key: "workflow", value: selectedWorkflow?.name || detail?.defaultWorkflowName || "尚未绑定工作流" },
                  { key: "memory", value: enableMemory ? `${enableLongTermMemory ? "长期" : ""}${enableShortTermMemory ? "短期" : ""}` || "已启用" : "未启用" }
                ]}
                size="small"
                align="left"
              />
            </div>
          </div>
        </section>

        <aside className="module-studio__coze-agent-preview">
          <div className="module-studio__section-head">
            <div>
              <Typography.Title heading={5} style={{ margin: 0 }}>预览与调试</Typography.Title>
              <Typography.Text type="tertiary">真实会话消息、模型流式响应和工作流运行痕迹都在这里查看。</Typography.Text>
            </div>
            <Tag color={conversationId ? "green" : "grey"}>{conversationId ? "已创建会话" : "未创建会话"}</Tag>
          </div>

          <div className="module-studio__message-list" data-testid="app-bot-ide-messages">
            {messages.length === 0 ? (
              <Empty title="暂无会话消息" image={null} />
            ) : (
              messages.map(message => (
                <article key={message.id} className={`module-studio__message module-studio__message--${message.role}`}>
                  <div className="module-studio__message-head">
                    <strong>{message.role}</strong>
                    <span>{formatDate(message.createdAt)}</span>
                  </div>
                  <pre className="module-studio__message-content">{message.content}</pre>
                </article>
              ))
            )}
            {streamingAssistant ? (
              <article className="module-studio__message module-studio__message--assistant">
                <div className="module-studio__message-head">
                  <strong>assistant</strong>
                  <span>streaming</span>
                </div>
                <pre className="module-studio__message-content">{streamingAssistant}</pre>
              </article>
            ) : null}
          </div>

          <Space wrap>
            <Button theme="borderless" onClick={() => void handleClearConversationContext()} disabled={!conversationId || saving || workbenchLoading}>
              清空上下文
            </Button>
            <Button theme="borderless" onClick={() => void handleClearConversationHistory()} disabled={!conversationId || saving || workbenchLoading}>
              清空历史
            </Button>
            <Button theme="borderless" type="danger" onClick={() => void handleDeleteConversation()} disabled={!conversationId || saving || workbenchLoading}>
              删除会话
            </Button>
          </Space>

          <textarea
            value={messageInput}
            onChange={event => setMessageInput(event.target.value)}
            rows={4}
            className="module-studio__textarea"
            placeholder="输入消息，测试当前智能体是否能通过已绑定模型正常回复。"
            disabled={!canChat || saving || workbenchLoading || sending || Boolean(workbenchError)}
            data-testid="app-bot-ide-message-input"
          />
          <Button theme="solid" type="primary" onClick={() => void handleSendMessage()} loading={sending} disabled={!canChat || saving || workbenchLoading || Boolean(workbenchError)} data-testid="app-bot-ide-send">
            发送消息
          </Button>

          <div className="module-studio__coze-agent-runbox">
            <Typography.Title heading={6}>工作流试运行</Typography.Title>
            <textarea
              value={workflowInput}
              onChange={event => setWorkflowInput(event.target.value)}
              rows={4}
              className="module-studio__textarea"
              placeholder="输入事件描述，例如：主机检测到可疑 PowerShell 横向移动行为，需要立即安排处置。"
              disabled={!workflowId || saving || workbenchLoading || runningWorkflow || Boolean(workbenchError)}
              data-testid="app-bot-ide-workflow-input"
            />
            <Button onClick={() => void handleRunWorkflow()} loading={runningWorkflow} disabled={!workflowId || saving || workbenchLoading || Boolean(workbenchError)} data-testid="app-bot-ide-run-workflow">
              执行安全事件任务
            </Button>
          </div>

          <div className="module-studio__trace-panel" data-testid="app-bot-ide-trace">
            {thoughts.length > 0 ? (
              <div className="module-studio__stack">
                <strong>模型思考摘要</strong>
                {thoughts.map((thought, index) => (
                  <Typography.Text key={`${thought}-${index}`} type="tertiary">{thought}</Typography.Text>
                ))}
              </div>
            ) : null}
            {lastTrace ? (
              <div className="module-studio__stack">
                <strong>最近一次工作流 Trace</strong>
                <span>Execution: {lastTrace.executionId}</span>
                <span>状态: {lastTrace.status || "-"}</span>
                {lastTrace.steps.map(step => (
                  <div key={step.nodeKey} className="module-studio__trace-step">
                    <strong>{step.nodeKey}</strong>
                    <span>{step.nodeType || "-"}</span>
                    <span>{step.status || "-"}</span>
                    <span>{step.durationMs ? `${step.durationMs} ms` : "-"}</span>
                    {step.errorMessage ? <Typography.Text type="danger">{step.errorMessage}</Typography.Text> : null}
                  </div>
                ))}
              </div>
            ) : (
              <Typography.Text type="tertiary">还没有工作流运行记录。</Typography.Text>
            )}
          </div>
        </aside>
      </div>
    </Surface>
  );
}

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
  const [kind, setKind] = useState<"form" | "sql" | "workflow">("workflow");
  const [description, setDescription] = useState("");
  const [result, setResult] = useState("");

  return (
    <Surface title="AI Assistant" subtitle="真实调用应用级 AI 辅助生成。" testId="app-ai-assistant-page">
      <div className="module-studio__stack">
        <div className="module-studio__actions">
          <Button theme={kind === "workflow" ? "solid" : "borderless"} onClick={() => setKind("workflow")}>Workflow</Button>
          <Button theme={kind === "form" ? "solid" : "borderless"} onClick={() => setKind("form")}>Form</Button>
          <Button theme={kind === "sql" ? "solid" : "borderless"} onClick={() => setKind("sql")}>SQL</Button>
        </div>
        <textarea value={description} onChange={event => setDescription(event.target.value)} rows={8} className="module-studio__textarea" />
        <Button onClick={() => void api.generateAssistant(kind, description).then(next => setResult(next?.result || ""))}>Generate</Button>
        <textarea value={result} rows={10} readOnly className="module-studio__textarea" />
      </div>
    </Surface>
  );
}

export function ModelConfigsPage({ api }: StudioPageProps) {
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
              <span className="module-studio__meta">{formatModelConfigEndpointSummary(item.baseUrl)}</span>
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
          </div>
        </div>
      </Modal>
    </Surface>
  );
}

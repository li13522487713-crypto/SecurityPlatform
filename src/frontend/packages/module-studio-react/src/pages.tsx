import { useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import {
  Banner,
  Button,
  Descriptions,
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
  ChatMessageItem,
  ConversationItem,
  DevelopFocus,
  DevelopResourceSummary,
  ModelConfigConnectionTestRequest,
  ModelConfigCreateRequest,
  ModelConfigItem,
  ModelConfigPromptTestRequest,
  ModelConfigStats,
  ModelConfigUpdateRequest,
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

export function DevelopPage({
  api,
  focus = "overview",
  workflowItems,
  chatflowItems,
  onOpenBot,
  onOpenWorkflow,
  onOpenChatflow,
  onOpenWorkflows,
  onOpenChatflows,
  onOpenAgentChat,
  onOpenModelConfigs,
  onCreateWorkflow,
  onCreateChatflow
}: StudioPageProps & {
  focus?: DevelopFocus;
  workflowItems: DevelopResourceSummary[];
  chatflowItems: DevelopResourceSummary[];
  onOpenBot: (botId: string) => void;
  onOpenWorkflow: (workflowId: string) => void;
  onOpenChatflow: (workflowId: string) => void;
  onOpenWorkflows: () => void;
  onOpenChatflows: () => void;
  onOpenAgentChat: () => void;
  onOpenModelConfigs: () => void;
  onCreateWorkflow: () => void;
  onCreateChatflow: () => void;
}) {
  const [items, setItems] = useState<AgentListItem[]>([]);
  const [models, setModels] = useState<ModelConfigItem[]>([]);
  const [activeFocus, setActiveFocus] = useState<DevelopFocus>(focus);
  const [keyword, setKeyword] = useState("");

  const load = async () => {
    const [agentsResult, modelResult] = await Promise.all([
      api.listAgents({ pageIndex: 1, pageSize: 20, keyword: keyword || undefined }),
      api.listModelConfigs()
    ]);
    setItems(agentsResult.items);
    setModels(modelResult.items);
  };

  useEffect(() => {
    void load();
  }, [keyword]);

  useEffect(() => {
    setActiveFocus(focus);
  }, [focus]);

  const recentResources = useMemo(() => {
    const recentAgents = items.slice(0, 3).map((item) => ({
      id: item.id,
      kind: "agent" as const,
      title: item.name,
      description: item.description || item.modelName || "Agent",
      updatedAt: item.createdAt,
      meta: item.status
    }));

    return [...recentAgents, ...workflowItems.slice(0, 3), ...chatflowItems.slice(0, 3)]
      .sort((left, right) => String(right.updatedAt || "").localeCompare(String(left.updatedAt || "")))
      .slice(0, 8);
  }, [chatflowItems, items, workflowItems]);

  const summaryCards = [
    {
      key: "agents",
      title: "Agent",
      description: "配置智能体、提示词和对话调试。",
      count: items.length,
      actionLabel: "进入 Agent",
      onClick: () => setActiveFocus("agents")
    },
    {
      key: "workflow",
      title: "Workflow",
      description: "编排自动化流程与测试运行。",
      count: workflowItems.length,
      actionLabel: "进入 Workflow",
      onClick: onOpenWorkflows
    },
    {
      key: "chatflow",
      title: "Chatflow",
      description: "面向对话流的编排与发布。",
      count: chatflowItems.length,
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
    }
  ];

  const filteredAgents = useMemo(
    () => items.filter((item) => !keyword || item.name.toLowerCase().includes(keyword.toLowerCase())),
    [items, keyword]
  );
  const filteredWorkflows = useMemo(
    () => workflowItems.filter((item) => !keyword || item.title.toLowerCase().includes(keyword.toLowerCase())),
    [keyword, workflowItems]
  );
  const filteredChatflows = useMemo(
    () => chatflowItems.filter((item) => !keyword || item.title.toLowerCase().includes(keyword.toLowerCase())),
    [chatflowItems, keyword]
  );

  async function handleCreateAgent() {
    const botId = await api.createAgent({
      name: `Agent_${Date.now().toString().slice(-6)}`,
      description: "通过 Develop 中心创建"
    });
    onOpenBot(botId);
  }

  return (
    <Surface
      title="Develop"
      subtitle="应用端 Coze 风格开发台，统一管理 Agent、Workflow、Chatflow 与模型配置。"
      testId="app-develop-page"
      toolbar={
        <Space>
          <Button icon={<IconPlus />} onClick={() => void handleCreateAgent()} data-testid="app-develop-create-agent">新建 Agent</Button>
          <Button onClick={onCreateWorkflow} data-testid="app-develop-create-workflow">新建 Workflow</Button>
          <Button onClick={onCreateChatflow} data-testid="app-develop-create-chatflow">新建 Chatflow</Button>
        </Space>
      }
    >
      <div className="module-studio__develop-stack">
        <div className="module-studio__hero">
          <div>
            <Typography.Title heading={3} style={{ margin: 0 }}>连续开发台</Typography.Title>
            <Typography.Text type="tertiary">从这里进入应用端 Agent、Workflow、Chatflow、模型配置与对话调试，不回平台侧。</Typography.Text>
          </div>
          <Space>
            <Button theme="solid" type="primary" onClick={onOpenAgentChat}>进入对话调试</Button>
            <Button onClick={onOpenModelConfigs}>模型配置</Button>
          </Space>
        </div>

        <CardGrid
          testId="app-develop-summary-grid"
          items={summaryCards}
          render={(item) => (
            <article key={item.key} className="module-studio__card module-studio__summary-card">
              <Tag color="light-blue">{item.title}</Tag>
              <strong>{item.count}</strong>
              <p>{item.description}</p>
              <Button onClick={item.onClick}>{item.actionLabel}</Button>
            </article>
          )}
        />

        <Banner
          type="info"
          bordered={false}
          fullMode={false}
          title="创建建议"
          description="Workflow 与 Chatflow 均通过模板化入口创建，后续在独立编辑器中完成调试、保存与发布。"
        />

        <div className="module-studio__develop-toolbar">
          <Input
            value={keyword}
            onChange={setKeyword}
            placeholder="搜索 Agent / Workflow / Chatflow"
            showClear
            data-testid="app-develop-search"
          />
          <Radio.Group type="button" value={activeFocus} onChange={(event) => setActiveFocus(event.target.value as DevelopFocus)}>
            <Radio value="overview">总览</Radio>
            <Radio value="agents">Agent</Radio>
            <Radio value="workflow">Workflow</Radio>
            <Radio value="chatflow">Chatflow</Radio>
            <Radio value="models">模型</Radio>
            <Radio value="chat">对话</Radio>
          </Radio.Group>
        </div>

        {(activeFocus === "overview" || activeFocus === "agents") ? (
          <section className="module-studio__section-block">
            <div className="module-studio__section-head">
              <Typography.Title heading={6}>Agent</Typography.Title>
              <Button theme="borderless" onClick={() => setActiveFocus("agents")}>查看全部</Button>
            </div>
            <CardGrid
              testId="app-develop-agents-grid"
              items={filteredAgents}
              render={(item: AgentListItem) => (
                <article key={item.id} className="module-studio__card">
                  <div className="module-studio__card-head">
                    <strong>{item.name}</strong>
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
          <section className="module-studio__section-block">
            <div className="module-studio__section-head">
              <Typography.Title heading={6}>Workflow</Typography.Title>
              <Space>
                <Button onClick={onCreateWorkflow}>新建 Workflow</Button>
                <Button theme="borderless" onClick={onOpenWorkflows}>资源列表</Button>
              </Space>
            </div>
            <CardGrid
              testId="app-develop-workflows-grid"
              items={filteredWorkflows}
              render={(item: DevelopResourceSummary) => (
                <article key={item.id} className="module-studio__card">
                  <div className="module-studio__card-head">
                    <strong>{item.title}</strong>
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
          <section className="module-studio__section-block">
            <div className="module-studio__section-head">
              <Typography.Title heading={6}>Chatflow</Typography.Title>
              <Space>
                <Button onClick={onCreateChatflow}>新建 Chatflow</Button>
                <Button theme="borderless" onClick={onOpenChatflows}>资源列表</Button>
              </Space>
            </div>
            <CardGrid
              testId="app-develop-chatflows-grid"
              items={filteredChatflows}
              render={(item: DevelopResourceSummary) => (
                <article key={item.id} className="module-studio__card">
                  <div className="module-studio__card-head">
                    <strong>{item.title}</strong>
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

        {(activeFocus === "overview" || activeFocus === "models") ? (
          <section className="module-studio__section-block">
            <div className="module-studio__section-head">
              <Typography.Title heading={6}>模型配置</Typography.Title>
              <Button theme="borderless" onClick={onOpenModelConfigs}>管理模型</Button>
            </div>
            <CardGrid
              testId="app-develop-models-grid"
              items={models}
              render={(item: ModelConfigItem) => (
                <article key={item.id} className="module-studio__card">
                  <div className="module-studio__card-head">
                    <strong>{item.name}</strong>
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
          <section className="module-studio__section-block">
            <div className="module-studio__section-head">
              <Typography.Title heading={6}>最近编辑</Typography.Title>
              <Button theme="borderless" onClick={onOpenAgentChat}>进入对话调试</Button>
            </div>
            <CardGrid
              testId="app-develop-recent-grid"
              items={recentResources}
              render={(item: DevelopResourceSummary) => (
                <article key={`${item.kind}-${item.id}`} className="module-studio__card module-studio__recent-card">
                  <Tag color={item.kind === "chatflow" ? "purple" : item.kind === "workflow" ? "blue" : item.kind === "model" ? "green" : "cyan"}>
                    {item.kind}
                  </Tag>
                  <strong>{item.title}</strong>
                  <p>{item.description || item.meta || "最近访问资源"}</p>
                  <span className="module-studio__meta">最近时间：{formatDate(item.updatedAt)}</span>
                  {item.kind === "agent" ? <Button onClick={() => onOpenBot(item.id)}>继续编辑</Button> : null}
                  {item.kind === "workflow" ? <Button onClick={() => onOpenWorkflow(item.id)}>继续编辑</Button> : null}
                  {item.kind === "chatflow" ? <Button onClick={() => onOpenChatflow(item.id)}>继续编辑</Button> : null}
                  {item.kind === "model" ? <Button onClick={onOpenModelConfigs}>查看模型</Button> : null}
                </article>
              )}
            />
          </section>
        ) : null}
      </div>
    </Surface>
  );
}

export function BotIdePage({ api, botId }: StudioPageProps & { botId: string }) {
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [systemPrompt, setSystemPrompt] = useState("");
  const [modelConfigId, setModelConfigId] = useState<string | undefined>(undefined);
  const [modelConfigs, setModelConfigs] = useState<ModelConfigItem[]>([]);

  useEffect(() => {
    void Promise.all([api.getAgent(botId), api.listModelConfigs()]).then(([detail, modelResult]) => {
      setName(detail.name);
      setDescription(detail.description || "");
      setSystemPrompt(detail.systemPrompt || "");
      setModelConfigId(detail.modelConfigId);
      setModelConfigs(modelResult.items);
    });
  }, [api, botId]);

  const selectedModel = modelConfigs.find(item => String(item.id) === String(modelConfigId ?? ""));

  return (
    <Surface title="Agent IDE" subtitle="Bot 配置、提示词与后续工作流绑定入口。" testId="app-bot-ide-page">
      <Banner type="info" bordered={false} fullMode={false} title="开发提示" description="这里已经接入应用端模型配置，可直接把 Agent 绑定到可用模型；后续继续补强工作流、知识库和工具挂接。" />
      <div className="module-studio__ide-layout">
        <div className="module-studio__stack">
          <Input value={name} onChange={setName} placeholder="Agent 名称" />
          <Input value={description} onChange={setDescription} placeholder="Agent 描述" />
          <Select
            value={modelConfigId}
            placeholder="选择模型配置"
            optionList={modelConfigs.map(item => ({
              label: `${item.name} / ${item.defaultModel}`,
              value: String(item.id)
            }))}
            onChange={value => setModelConfigId(typeof value === "string" ? value : undefined)}
            data-testid="app-bot-ide-model-config"
          />
          <textarea value={systemPrompt} onChange={event => setSystemPrompt(event.target.value)} rows={12} className="module-studio__textarea" />
          <Button
            theme="solid"
            type="primary"
            onClick={() =>
              void api.updateAgent(botId, {
                name,
                description,
                systemPrompt,
                modelConfigId,
                modelName: selectedModel?.defaultModel
              })
            }
          >
            保存配置
          </Button>
        </div>
        <aside className="module-studio__ide-sidecard">
          <Typography.Title heading={6}>当前绑定</Typography.Title>
          <Typography.Text type="tertiary">
            {selectedModel ? `${selectedModel.providerType} / ${selectedModel.defaultModel}` : "尚未绑定模型配置"}
          </Typography.Text>
          <Descriptions
            data={[
              { key: "model", value: selectedModel?.name || "-" },
              { key: "embedding", value: selectedModel?.supportsEmbedding ? "支持 Embedding" : "普通推理" },
              { key: "tools", value: selectedModel?.enableTools ? "已启用工具" : "未启用工具" }
            ]}
            size="small"
            align="left"
          />
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
            render={(item: ChatMessageItem) => <article key={item.id} className="module-studio__card"><strong>{item.role}</strong><p>{item.content}</p></article>}
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
              <span className="module-studio__meta">Base URL: {item.baseUrl || "-"}</span>
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

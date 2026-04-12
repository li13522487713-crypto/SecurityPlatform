import { useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import { Banner, Button, Empty, Input, Radio, Space, Tag, Typography } from "@douyinfe/semi-ui";
import { IconPlus } from "@douyinfe/semi-icons";
import type {
  AgentListItem,
  ChatMessageItem,
  ConversationItem,
  DevelopFocus,
  DevelopResourceSummary,
  ModelConfigItem,
  StudioPageProps
} from "./types";

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

  useEffect(() => {
    void api.getAgent(botId).then(detail => {
      setName(detail.name);
      setDescription(detail.description || "");
      setSystemPrompt(detail.systemPrompt || "");
    });
  }, [api, botId]);

  return (
    <Surface title="Agent IDE" subtitle="Bot 配置、提示词与后续工作流绑定入口。" testId="app-bot-ide-page">
      <Banner type="info" bordered={false} fullMode={false} title="开发提示" description="这里保持为应用端 Agent IDE，不回平台侧；后续可继续补强工作流绑定和调试区。" />
      <div className="module-studio__ide-layout">
        <div className="module-studio__stack">
          <Input value={name} onChange={setName} placeholder="Agent 名称" />
          <Input value={description} onChange={setDescription} placeholder="Agent 描述" />
          <textarea value={systemPrompt} onChange={event => setSystemPrompt(event.target.value)} rows={12} className="module-studio__textarea" />
          <Button theme="solid" type="primary" onClick={() => void api.updateAgent(botId, { name, description, systemPrompt })}>保存配置</Button>
        </div>
        <aside className="module-studio__ide-sidecard">
          <Typography.Title heading={6}>调试工作台</Typography.Title>
          <Typography.Text type="tertiary">后续将继续补强模型选择、会话预览、工作流绑定与知识库挂接。</Typography.Text>
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

  useEffect(() => {
    void api.listModelConfigs().then(result => setItems(result.items));
  }, [api]);

  return (
    <Surface title="Model Configs" subtitle="模型配置列表" testId="app-model-configs-page">
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
          </article>
        )}
      />
    </Surface>
  );
}

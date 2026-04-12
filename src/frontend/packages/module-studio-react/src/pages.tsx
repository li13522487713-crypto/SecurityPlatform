import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { Button, Empty, Input, Typography } from "@douyinfe/semi-ui";
import { IconPlus } from "@douyinfe/semi-icons";
import type { AgentListItem, ChatMessageItem, ConversationItem, StudioPageProps } from "./types";

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

export function DevelopPage({ api, onOpenBot }: StudioPageProps & { onOpenBot: (botId: string) => void }) {
  const [items, setItems] = useState<AgentListItem[]>([]);

  const load = async () => {
    const result = await api.listAgents({ pageIndex: 1, pageSize: 20 });
    setItems(result.items);
  };

  useEffect(() => {
    void load();
  }, []);

  return (
    <Surface
      title="Develop"
      subtitle="Coze 风格开发台"
      testId="app-develop-page"
      toolbar={<Button icon={<IconPlus />} onClick={() => void api.createAgent({ name: "New Agent", description: "Created in app-web" }).then(() => load())}>Create Agent</Button>}
    >
      <CardGrid
        testId="app-agent-mgmt-page"
        items={items}
        render={(item: AgentListItem) => (
          <article key={item.id} className="module-studio__card">
            <strong>{item.name}</strong>
            <p>{item.description || item.modelName || "-"}</p>
            <Button onClick={() => onOpenBot(item.id)}>Open</Button>
          </article>
        )}
      />
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
    <Surface title="Agent IDE" subtitle="Bot 详情与提示词配置" testId="app-bot-ide-page">
      <div className="module-studio__stack">
        <Input value={name} onChange={setName} />
        <Input value={description} onChange={setDescription} />
        <textarea value={systemPrompt} onChange={event => setSystemPrompt(event.target.value)} rows={10} className="module-studio__textarea" />
        <Button onClick={() => void api.updateAgent(botId, { name, description, systemPrompt })}>Save</Button>
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
    <Surface title="Agent Chat" subtitle="查看 Agent 对话历史" testId="app-agent-chat-page">
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
    <Surface title="AI Assistant" subtitle="真实调用应用级 AI 辅助生成" testId="app-ai-assistant-page">
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
  const [items, setItems] = useState<Array<{ id: number; name: string; providerType: string; defaultModel: string }>>([]);

  useEffect(() => {
    void api.listModelConfigs().then(result => setItems(result.items));
  }, [api]);

  return (
    <Surface title="Model Configs" subtitle="模型配置列表" testId="app-model-configs-page">
      <CardGrid
        testId="app-model-configs-grid"
        items={items}
        render={(item: { id: number; name: string; providerType: string; defaultModel: string }) => <article key={item.id} className="module-studio__card"><strong>{item.name}</strong><p>{item.providerType} / {item.defaultModel}</p></article>}
      />
    </Surface>
  );
}

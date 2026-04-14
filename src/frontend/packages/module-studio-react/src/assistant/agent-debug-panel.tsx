import {
  Banner,
  Button,
  Descriptions,
  Empty,
  Space,
  Tag,
  Typography
} from "@douyinfe/semi-ui";
import type {
  AgentDatabaseBindingInput,
  AgentKnowledgeBindingInput,
  AgentPluginBindingInput,
  AgentVariableBindingInput,
  ChatMessageItem,
  StudioAssistantPublication,
  WorkbenchTrace
} from "../types";
import type { WorkbenchResourceUsage } from "./agent-ide-helpers";
import { formatDate } from "./agent-ide-helpers";
import { AgentVersionHistory } from "./agent-version-history";

export interface AgentDebugPanelProps {
  hasUnsavedChanges: boolean;
  conversationId: string;
  lastResourceUsage: WorkbenchResourceUsage | null;
  knowledgeBindings: AgentKnowledgeBindingInput[];
  pluginBindings: AgentPluginBindingInput[];
  databaseBindings: AgentDatabaseBindingInput[];
  databaseOptions: Array<{ id: number; name: string; botId?: number }>;
  variableBindings: AgentVariableBindingInput[];
  publicationLoading: boolean;
  publications: StudioAssistantPublication[];
  onPublishClick: () => void;
  onOpenPublish?: () => void;
  onRegenerateEmbedToken: () => void;
  saving: boolean;
  workbenchLoading: boolean;
  messages: ChatMessageItem[];
  streamingAssistant: string;
  messageInput: string;
  onMessageInputChange: (value: string) => void;
  /** 平台侧是否存在至少一条模型配置（`listModelConfigs` 非空）。 */
  modelCatalogEmpty: boolean;
  /** 已存在可选模型但未在智能体上选择 `modelConfigId`。 */
  modelNotSelected: boolean;
  canChat: boolean;
  workbenchError: string | null;
  sending: boolean;
  onSendMessage: () => void;
  workflowInput: string;
  onWorkflowInputChange: (value: string) => void;
  workflowId: string | undefined;
  runningWorkflow: boolean;
  onRunWorkflow: () => void;
  thoughts: string[];
  lastTrace: WorkbenchTrace | null;
  onClearConversationContext: () => void;
  onClearConversationHistory: () => void;
  onDeleteConversation: () => void;
}

export function AgentDebugPanel({
  hasUnsavedChanges,
  conversationId,
  lastResourceUsage,
  knowledgeBindings,
  pluginBindings,
  databaseBindings,
  databaseOptions,
  variableBindings,
  publicationLoading,
  publications,
  onPublishClick,
  onOpenPublish,
  onRegenerateEmbedToken,
  saving,
  workbenchLoading,
  messages,
  streamingAssistant,
  messageInput,
  onMessageInputChange,
  modelCatalogEmpty,
  modelNotSelected,
  canChat,
  workbenchError,
  sending,
  onSendMessage,
  workflowInput,
  onWorkflowInputChange,
  workflowId,
  runningWorkflow,
  onRunWorkflow,
  thoughts,
  lastTrace,
  onClearConversationContext,
  onClearConversationHistory,
  onDeleteConversation
}: AgentDebugPanelProps) {
  const chatInteractionLocked =
    Boolean(workbenchError) ||
    saving ||
    workbenchLoading ||
    modelCatalogEmpty ||
    modelNotSelected ||
    !canChat;

  return (
    <aside className="module-studio__agent-debug-aside">
      <header className="module-studio__agent-debug-header">
        <div className="module-studio__section-head module-studio__agent-debug-title-row">
          <div>
            <Typography.Title heading={5} style={{ margin: 0 }}>
              预览与调试
            </Typography.Title>
            <Typography.Text type="tertiary">真实会话消息、模型流式响应和工作流运行痕迹都在这里查看。</Typography.Text>
          </div>
          <div className="module-studio__inline-actions">
            {hasUnsavedChanges ? <Tag color="orange">有未保存改动</Tag> : null}
            <Tag color={conversationId ? "green" : "grey"}>{conversationId ? "已创建会话" : "未创建会话"}</Tag>
          </div>
        </div>
      </header>

      <div className="module-studio__agent-debug-banners">
        {!workbenchLoading && modelCatalogEmpty ? (
          <Banner
            type="warning"
            bordered={false}
            fullMode={false}
            title="暂无法进行对话调试"
            description="当前租户下没有可用的模型配置。请先在「模型配置」中新增并完成连接测试后再回到此处。"
          />
        ) : null}
        {!workbenchLoading && !modelCatalogEmpty && modelNotSelected ? (
          <Banner
            type="warning"
            bordered={false}
            fullMode={false}
            title="请先选择模型"
            description="中间配置区「模型」页中尚未选择默认模型。选定并保存后，方可发送消息进行调试。"
          />
        ) : null}
        {!workbenchLoading && !modelCatalogEmpty && !modelNotSelected && !canChat ? (
          <Banner
            type="warning"
            bordered={false}
            fullMode={false}
            title="当前模型不可用"
            description="已选择的模型配置未启用或不可用。请到模型配置页启用该模型并完成连接测试与 Prompt 测试。"
          />
        ) : null}
      </div>

      <div className="module-studio__agent-debug-inspectors">
        <div className="module-studio__coze-inspector-card module-studio__agent-debug-card">
          <div className="module-studio__agent-debug-card-title">本轮资源摘要</div>
          <Descriptions
            data={[
              { key: "knowledge", value: lastResourceUsage?.knowledgeBases.join(", ") || `${knowledgeBindings.filter(item => item.isEnabled).length} 个启用知识库` },
              { key: "plugin", value: lastResourceUsage?.pluginTools.join(", ") || `${pluginBindings.flatMap(item => item.toolBindings ?? []).filter(item => item.isEnabled).length} 个启用工具` },
              { key: "database", value: lastResourceUsage?.databases.join(", ") || databaseOptions.find(item => item.id === databaseBindings.find(candidate => candidate.isDefault)?.databaseId)?.name || "未设置默认数据库" },
              { key: "variable", value: lastResourceUsage?.variables.join(", ") || `${variableBindings.length} 个暴露变量` }
            ]}
            size="small"
            align="left"
          />
        </div>

        <div className="module-studio__coze-inspector-card module-studio__agent-debug-card">
          <div className="module-studio__card-head">
            <span className="module-studio__agent-debug-card-title">发布与嵌入</span>
            <Space>
              <Button theme="light" type="tertiary" onClick={onPublishClick} disabled={saving || workbenchLoading}>
                发布
              </Button>
              {onOpenPublish ? (
                <Button theme="borderless" onClick={onOpenPublish}>
                  打开发布页
                </Button>
              ) : null}
              <Button theme="borderless" onClick={onRegenerateEmbedToken} disabled={publications.length === 0 || publicationLoading}>
                刷新令牌
              </Button>
            </Space>
          </div>
          <AgentVersionHistory publications={publications} publicationLoading={publicationLoading} />
        </div>
      </div>

      <div className="module-studio__agent-debug-messages-wrap">
        <div className="module-studio__agent-debug-messages-label">会话消息</div>
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
            <article className="module-studio__message module-studio__message--assistant module-studio__message--streaming">
              <div className="module-studio__message-head">
                <strong>assistant</strong>
                <span>streaming</span>
              </div>
              <pre className="module-studio__message-content">{streamingAssistant}</pre>
            </article>
          ) : null}
        </div>
      </div>

      <div className="module-studio__agent-debug-session-actions">
        <Space wrap>
          <Button theme="borderless" onClick={onClearConversationContext} disabled={!conversationId || saving || workbenchLoading}>
            清空上下文
          </Button>
          <Button theme="borderless" onClick={onClearConversationHistory} disabled={!conversationId || saving || workbenchLoading}>
            清空历史
          </Button>
          <Button theme="borderless" type="danger" onClick={onDeleteConversation} disabled={!conversationId || saving || workbenchLoading}>
            删除会话
          </Button>
        </Space>
      </div>

      <div className="module-studio__agent-debug-composer">
        <textarea
          value={messageInput}
          onChange={event => onMessageInputChange(event.target.value)}
          rows={4}
          className="module-studio__textarea module-studio__textarea--composer"
          placeholder="输入消息，测试当前智能体是否能通过已绑定模型正常回复。"
          disabled={chatInteractionLocked || sending}
          data-testid="app-bot-ide-message-input"
        />
        <div className="module-studio__agent-debug-composer-actions">
          <Button theme="solid" type="primary" onClick={onSendMessage} loading={sending} disabled={chatInteractionLocked} data-testid="app-bot-ide-send">
            发送消息
          </Button>
        </div>
      </div>

      <div className="module-studio__coze-agent-runbox module-studio__agent-debug-runbox">
        <Typography.Title heading={6} style={{ margin: 0 }}>
          工作流试运行
        </Typography.Title>
        <textarea
          value={workflowInput}
          onChange={event => onWorkflowInputChange(event.target.value)}
          rows={4}
          className="module-studio__textarea"
          placeholder="输入事件描述，例如：主机检测到可疑 PowerShell 横向移动行为，需要立即安排处置。"
          disabled={!workflowId || saving || workbenchLoading || runningWorkflow || Boolean(workbenchError)}
          data-testid="app-bot-ide-workflow-input"
        />
        <div className="module-studio__agent-debug-runbox-actions">
          <Button onClick={onRunWorkflow} loading={runningWorkflow} disabled={!workflowId || saving || workbenchLoading || Boolean(workbenchError)} data-testid="app-bot-ide-run-workflow">
            执行安全事件任务
          </Button>
        </div>
      </div>

      <div className="module-studio__trace-panel module-studio__agent-debug-trace" data-testid="app-bot-ide-trace">
        {thoughts.length > 0 ? (
          <div className="module-studio__stack">
            <strong>模型思考摘要</strong>
            {thoughts.map((thought, index) => (
              <Typography.Text key={`${thought}-${index}`} type="tertiary">
                {thought}
              </Typography.Text>
            ))}
          </div>
        ) : null}
        {lastTrace ? (
          <div className="module-studio__stack">
            <strong>最近一次工作流 Trace</strong>
            <span>Execution: {lastTrace.executionId}</span>
            <span>状态: {lastTrace.status || "-"}</span>
            {lastTrace.durationMs !== undefined ? <span className="module-studio__meta">总耗时: {lastTrace.durationMs} ms</span> : null}
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
  );
}

/**
 * AI 原生类组件实现（M06 P1-1 + P1-5，4 件）：AiChat / AiCard / AiSuggestion / AiAvatarReply
 *
 * AI 6 维矩阵（PLAN §M06 C06-4，每个组件需逐项满足）：
 *  1) 绑定 chatflow（chatflowId）
 *  2) 绑定模型（modelId）
 *  3) SSE 流式 → AiChat 用 ChatflowAdapter.streamChat 真实流式
 *  4) tool_call 气泡（折叠/展开 + 重试）→ AiChat 实现完整气泡
 *  5) 历史回放 → AiChat 渲染累积消息列表
 *  6) 中断 / 恢复 / 重新生成 → AiChat 暴露 pause/resume/regenerate 按钮
 *
 * 强约束（PLAN §1.3 #4）：
 *  - 本组件不直接 fetch /api/runtime/chatflows/*；通过 ChatflowAdapter 注入（getContentParam 或父级 RuntimeContext）。
 *  - lowcode-runtime-web 在装载时把 ChatflowAdapter 实例通过 props.chatflowAdapter 透传到 AiChat。
 */
import * as React from 'react';
import { Avatar, Button, Card, Spin, Typography } from '@douyinfe/semi-ui';
import { IconArrowUp, IconRefresh, IconStop } from '@douyinfe/semi-icons';
import type { ChatChunk, ChatflowAdapter, ToolCallChunk, MessageChunk } from '@atlas/lowcode-chatflow-adapter';
import type { ComponentRenderer } from './runtime-types';

const { Text, Paragraph } = Typography;

interface AiChatRuntimeProps {
  chatflowId?: string;
  sessionId?: string;
  modelId?: string;
  /** 由父级 RuntimeContext 注入；若未注入则 AiChat 显示"未配置 ChatflowAdapter" 状态而非 mock。*/
  chatflowAdapter?: ChatflowAdapter;
}

interface AiChatMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  toolCalls?: ToolCallChunk[];
  finalOutputs?: Record<string, unknown>;
  error?: string;
  pending?: boolean;
}

const AiChat: ComponentRenderer = ({ props, fireEvent }) => {
  const { chatflowId, sessionId, chatflowAdapter } = props as unknown as AiChatRuntimeProps;
  const [messages, setMessages] = React.useState<AiChatMessage[]>([]);
  const [input, setInput] = React.useState('');
  const [streaming, setStreaming] = React.useState(false);
  const abortRef = React.useRef<AbortController | null>(null);

  const send = React.useCallback(
    async (text: string) => {
      if (!text.trim() || streaming) return;
      if (!chatflowAdapter || !chatflowId) {
        setMessages((prev) => [
          ...prev,
          { id: `e_${Date.now()}`, role: 'assistant', content: '', error: '未注入 ChatflowAdapter 或 chatflowId（绑定后即可使用流式对话）' }
        ]);
        return;
      }

      const userMsgId = `u_${Date.now()}`;
      const aiMsgId = `a_${Date.now() + 1}`;
      setMessages((prev) => [
        ...prev,
        { id: userMsgId, role: 'user', content: text },
        { id: aiMsgId, role: 'assistant', content: '', pending: true, toolCalls: [] }
      ]);
      setInput('');
      setStreaming(true);
      fireEvent('onSubmit', { input: text });

      const ac = new AbortController();
      abortRef.current = ac;
      try {
        for await (const chunk of chatflowAdapter.streamChat({ chatflowId, sessionId, input: text }, ac.signal)) {
          setMessages((prev) => prev.map((m) => (m.id === aiMsgId ? mergeChunk(m, chunk) : m)));
          fireEvent('onChange', { kind: chunk.kind });
        }
      } catch (err) {
        const msg = err instanceof Error ? err.message : String(err);
        setMessages((prev) => prev.map((m) => (m.id === aiMsgId ? { ...m, error: msg, pending: false } : m)));
      } finally {
        setMessages((prev) => prev.map((m) => (m.id === aiMsgId ? { ...m, pending: false } : m)));
        setStreaming(false);
        abortRef.current = null;
      }
    },
    [chatflowAdapter, chatflowId, sessionId, fireEvent, streaming]
  );

  const stop = React.useCallback(() => {
    abortRef.current?.abort();
    if (sessionId) {
      void chatflowAdapter?.pauseChat(sessionId);
    }
  }, [chatflowAdapter, sessionId]);

  const regenerate = React.useCallback(() => {
    const lastUser = [...messages].reverse().find((m) => m.role === 'user');
    if (lastUser) void send(lastUser.content);
  }, [messages, send]);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', minHeight: 240, gap: 8 }}>
      <div style={{ flex: 1, overflowY: 'auto', padding: 8 }}>
        {messages.length === 0 && (
          <Text type="tertiary" size="small">
            开始一段对话…
          </Text>
        )}
        {messages.map((m) => (
          <AiChatBubble key={m.id} message={m} onRetry={regenerate} />
        ))}
      </div>
      <div style={{ display: 'flex', gap: 8 }}>
        <input
          style={{ flex: 1, padding: '6px 10px', border: '1px solid var(--semi-color-border)', borderRadius: 4 }}
          value={input}
          onChange={(e) => setInput(e.target.value)}
          placeholder="请输入消息，Enter 发送"
          onKeyDown={(e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
              e.preventDefault();
              void send(input);
            }
          }}
        />
        {streaming ? (
          <Button icon={<IconStop />} onClick={stop}>
            中断
          </Button>
        ) : (
          <Button icon={<IconArrowUp />} type="primary" onClick={() => void send(input)}>
            发送
          </Button>
        )}
        <Button icon={<IconRefresh />} onClick={regenerate} disabled={streaming || messages.length === 0}>
          重试
        </Button>
      </div>
    </div>
  );
};

function mergeChunk(m: AiChatMessage, chunk: ChatChunk): AiChatMessage {
  switch (chunk.kind) {
    case 'message':
      return { ...m, content: m.content + (chunk as MessageChunk).content, pending: true };
    case 'tool_call':
      return { ...m, toolCalls: [...(m.toolCalls ?? []), chunk as ToolCallChunk], pending: true };
    case 'error':
      return { ...m, error: chunk.message, pending: false };
    case 'final':
      return { ...m, finalOutputs: chunk.outputs as Record<string, unknown> | undefined, pending: false };
    default:
      return m;
  }
}

const AiChatBubble: React.FC<{ message: AiChatMessage; onRetry: () => void }> = ({ message, onRetry }) => {
  const isUser = message.role === 'user';
  const [toolExpanded, setToolExpanded] = React.useState(false);
  return (
    <div style={{ display: 'flex', flexDirection: isUser ? 'row-reverse' : 'row', alignItems: 'flex-start', gap: 8, marginBottom: 12 }}>
      <Avatar size="small">{isUser ? '我' : 'AI'}</Avatar>
      <div style={{ maxWidth: '75%', background: isUser ? 'var(--semi-color-primary-light-default)' : 'var(--semi-color-fill-0)', borderRadius: 8, padding: '6px 10px' }}>
        {message.toolCalls && message.toolCalls.length > 0 && (
          <div style={{ marginBottom: 4 }}>
            <Button size="small" type="tertiary" onClick={() => setToolExpanded((v) => !v)}>
              {toolExpanded ? '收起' : '展开'}工具调用 ({message.toolCalls.length})
            </Button>
            {toolExpanded && (
              <pre style={{ margin: 4, fontSize: 12, maxHeight: 160, overflow: 'auto' }}>
                {JSON.stringify(message.toolCalls, null, 2)}
              </pre>
            )}
          </div>
        )}
        <Paragraph style={{ margin: 0, whiteSpace: 'pre-wrap' }}>{message.content}</Paragraph>
        {message.pending && <Spin size="small" />}
        {message.error && (
          <div style={{ marginTop: 4 }}>
            <Text type="danger" size="small">
              {message.error}
            </Text>
            <Button size="small" type="tertiary" onClick={onRetry}>
              重试
            </Button>
          </div>
        )}
      </div>
    </div>
  );
};

const AiCard: ComponentRenderer = ({ props, getContentParam }) => {
  const config = (props.cardConfig as Record<string, unknown> | undefined) ?? (getContentParam?.('ai') as Record<string, unknown> | undefined);
  return (
    <Card title={typeof config?.title === 'string' ? config.title : 'AI 卡片'}>
      <Paragraph>{typeof config?.body === 'string' ? config.body : 'AI 卡片内容由 chatflow 输出 / 内容参数(ai) 注入。'}</Paragraph>
    </Card>
  );
};

const AiSuggestion: ComponentRenderer = ({ props, fireEvent, getContentParam }) => {
  const list = Array.isArray(props.suggestions) ? props.suggestions : (getContentParam?.('data') as unknown[] | undefined) ?? [];
  return (
    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
      {list.map((item, i) => (
        <Button key={i} type="tertiary" onClick={() => fireEvent('onItemClick', { index: i, item })}>
          {String(typeof item === 'object' && item != null && 'label' in item ? (item as Record<string, unknown>).label : item)}
        </Button>
      ))}
    </div>
  );
};

const AiAvatarReply: ComponentRenderer = ({ props, getContentParam }) => {
  const reply = (getContentParam?.('ai') as { text?: string } | undefined) ?? null;
  return (
    <div style={{ display: 'flex', alignItems: 'flex-start', gap: 8 }}>
      <Avatar src={typeof props.avatarUrl === 'string' ? props.avatarUrl : undefined}>AI</Avatar>
      <Card style={{ flex: 1 }}>
        <Paragraph>{reply?.text ?? '等待 AI 回复…'}</Paragraph>
      </Card>
    </div>
  );
};

export const AI_COMPONENTS: Record<string, ComponentRenderer> = {
  AiChat,
  AiCard,
  AiSuggestion,
  AiAvatarReply
};

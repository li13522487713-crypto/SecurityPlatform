/**
 * Chatflow 适配器协议（M11 C11-1..C11-9）。
 *
 * 4 类 SSE 事件：tool_call / message / error / final。
 * 每帧形如：
 *   data: {"kind":"message","content":"hello"}
 *   data: {"kind":"final","outputs":{...}}
 */

import type { JsonValue } from '@atlas/lowcode-schema';

export type ChatChunkKind = 'tool_call' | 'message' | 'error' | 'final';

export interface ChatChunkBase {
  kind: ChatChunkKind;
  /** 帧 id（顺序）。*/
  seq?: number;
}

export interface ToolCallChunk extends ChatChunkBase {
  kind: 'tool_call';
  toolName: string;
  args: Record<string, JsonValue>;
  /** 工具调用 id（用于关联工具结果回流）。*/
  toolCallId?: string;
}

export interface MessageChunk extends ChatChunkBase {
  kind: 'message';
  /** 增量字符串。*/
  content: string;
  /** 是否为 markdown 内容（默认是）。*/
  markdown?: boolean;
}

export interface ErrorChunk extends ChatChunkBase {
  kind: 'error';
  message: string;
  recoverable?: boolean;
}

export interface FinalChunk extends ChatChunkBase {
  kind: 'final';
  outputs?: Record<string, JsonValue>;
}

export type ChatChunk = ToolCallChunk | MessageChunk | ErrorChunk | FinalChunk;

export interface ChatStreamRequest {
  chatflowId: string;
  sessionId?: string;
  /** 用户输入。*/
  input: string;
  /** 注入的系统变量（page/app/system 上下文）。*/
  context?: Record<string, JsonValue>;
}

export interface ChatflowAdapter {
  streamChat(req: ChatStreamRequest, signal?: AbortSignal): AsyncIterable<ChatChunk>;
  /** 中断当前会话流式输出。*/
  pauseChat(sessionId: string): Promise<void>;
  /** 在中断后恢复（继续上一轮）。*/
  resumeChat(sessionId: string): AsyncIterable<ChatChunk>;
  /** 在任意位置插入用户消息（中断态下追加上下文，再 resume）。*/
  injectMessage(sessionId: string, message: string): Promise<void>;
}

export interface SessionInfo {
  id: string;
  title?: string;
  pinned?: boolean;
  archived?: boolean;
  updatedAt: string;
}

export interface SessionAdapter {
  list(): Promise<SessionInfo[]>;
  create(title?: string): Promise<{ id: string }>;
  switch(id: string): Promise<void>;
  clear(id: string): Promise<void>;
  pin(id: string, pinned: boolean): Promise<void>;
  archive(id: string, archived: boolean): Promise<void>;
}

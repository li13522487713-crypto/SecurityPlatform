/**
 * HttpChatflowAdapter（M11 C11-1..C11-4）：基于 fetch + ReadableStream 调用 RuntimeChatflowsController。
 */

import type { ChatChunk, ChatStreamRequest, ChatflowAdapter } from './types';
import { parseSseStream } from './sse';

const ROOT = '/api/runtime/chatflows';

export interface HttpChatflowAdapterOptions {
  tenantId: string;
  token?: string;
}

export class HttpChatflowAdapter implements ChatflowAdapter {
  constructor(private readonly opts: HttpChatflowAdapterOptions) {}

  async *streamChat(req: ChatStreamRequest, signal?: AbortSignal): AsyncIterable<ChatChunk> {
    const res = await fetch(`${ROOT}/${encodeURIComponent(req.chatflowId)}:invoke`, {
      method: 'POST',
      signal,
      headers: {
        'Content-Type': 'application/json',
        Accept: 'text/event-stream',
        'X-Tenant-Id': this.opts.tenantId,
        Authorization: this.opts.token ? `Bearer ${this.opts.token}` : ''
      },
      body: JSON.stringify(req)
    });
    if (!res.ok || !res.body) {
      const text = await res.text().catch(() => '');
      throw new Error(`chatflow stream ${res.status}: ${text}`);
    }
    yield* parseSseStream(res.body);
  }

  async pauseChat(sessionId: string): Promise<void> {
    await this.fetchJson(`${ROOT}/sessions/${encodeURIComponent(sessionId)}:pause`, 'POST');
  }

  async *resumeChat(sessionId: string): AsyncIterable<ChatChunk> {
    const res = await fetch(`${ROOT}/sessions/${encodeURIComponent(sessionId)}:resume`, {
      method: 'POST',
      headers: {
        Accept: 'text/event-stream',
        'X-Tenant-Id': this.opts.tenantId,
        Authorization: this.opts.token ? `Bearer ${this.opts.token}` : ''
      }
    });
    if (!res.ok || !res.body) {
      const text = await res.text().catch(() => '');
      throw new Error(`chatflow resume ${res.status}: ${text}`);
    }
    yield* parseSseStream(res.body);
  }

  async injectMessage(sessionId: string, message: string): Promise<void> {
    await this.fetchJson(`${ROOT}/sessions/${encodeURIComponent(sessionId)}:inject`, 'POST', { message });
  }

  private async fetchJson(path: string, method: string, body?: unknown): Promise<void> {
    const res = await fetch(path, {
      method,
      headers: {
        'Content-Type': 'application/json',
        'X-Tenant-Id': this.opts.tenantId,
        Authorization: this.opts.token ? `Bearer ${this.opts.token}` : ''
      },
      body: body ? JSON.stringify(body) : undefined
    });
    if (!res.ok) {
      const text = await res.text().catch(() => '');
      throw new Error(`chatflow ${method} ${path} ${res.status}: ${text}`);
    }
  }
}

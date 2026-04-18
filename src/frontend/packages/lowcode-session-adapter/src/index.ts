/**
 * @atlas/lowcode-session-adapter — Session 多会话适配器（M11 C11-6）。
 */

import type { SessionAdapter, SessionInfo } from '@atlas/lowcode-chatflow-adapter';

const ROOT = '/api/runtime/sessions';

interface ApiResponse<T> { success: boolean; data: T; code?: string; message?: string }

export interface HttpSessionAdapterOptions {
  tenantId: string;
  token?: string;
}

export class HttpSessionAdapter implements SessionAdapter {
  constructor(private readonly opts: HttpSessionAdapterOptions) {}

  list(): Promise<SessionInfo[]> {
    return this.fetchJson<SessionInfo[]>(ROOT, 'GET');
  }

  create(title?: string): Promise<{ id: string }> {
    return this.fetchJson<{ id: string }>(ROOT, 'POST', { title });
  }

  async switch(id: string): Promise<void> {
    await this.fetchJson<unknown>(`${ROOT}/${encodeURIComponent(id)}:switch`, 'POST');
  }

  async clear(id: string): Promise<void> {
    await this.fetchJson<unknown>(`${ROOT}/${encodeURIComponent(id)}/clear`, 'POST');
  }

  async pin(id: string, pinned: boolean): Promise<void> {
    await this.fetchJson<unknown>(`${ROOT}/${encodeURIComponent(id)}/pin`, 'POST', { pinned });
  }

  async archive(id: string, archived: boolean): Promise<void> {
    await this.fetchJson<unknown>(`${ROOT}/${encodeURIComponent(id)}/archive`, 'POST', { archived });
  }

  private async fetchJson<T>(path: string, method: string, body?: unknown): Promise<T> {
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
      throw new Error(`session ${method} ${path} ${res.status}: ${text}`);
    }
    const json = (await res.json()) as ApiResponse<T>;
    if (!json.success) throw new Error(`${json.code ?? 'SESSION_ERROR'}: ${json.message ?? 'unknown'}`);
    return json.data;
  }
}

export const __ATLAS_LOWCODE_PACKAGE__ = '@atlas/lowcode-session-adapter' as const;

/**
 * 运行时 dispatch 客户端（M08 + M13 联动）。
 *
 * 强约束（PLAN.md §1.3 #2）：
 * - 所有运行时事件必须经此客户端 → POST /api/runtime/events/dispatch；
 * - UI 禁止直调任何 /api/runtime/workflows / chatflows / files / triggers / sessions / plugins 端点。
 */

import type { ActionSchema, JsonValue, RuntimeStatePatch } from '@atlas/lowcode-schema';

export interface DispatchRequest {
  appId: string;
  pageId?: string;
  componentId?: string;
  eventName?: string;
  versionId?: string;
  inputs?: Record<string, JsonValue>;
  stateSnapshot?: JsonValue;
  /** 触发本次 dispatch 的动作链。*/
  actions: ActionSchema[];
}

export interface DispatchResponse {
  traceId: string;
  outputs?: Record<string, JsonValue>;
  statePatches?: RuntimeStatePatch[];
  messages?: ReadonlyArray<{ kind: 'info' | 'success' | 'warning' | 'error'; text: string }>;
  errors?: ReadonlyArray<{ kind: string; message: string; stack?: string }>;
}

export interface DispatchClient {
  send(req: DispatchRequest, signal?: AbortSignal): Promise<DispatchResponse>;
}

const DISPATCH_URL = '/api/runtime/events/dispatch';

export class HttpDispatchClient implements DispatchClient {
  constructor(private readonly tenantId: string, private readonly token?: string) {}

  async send(req: DispatchRequest, signal?: AbortSignal): Promise<DispatchResponse> {
    const res = await fetch(DISPATCH_URL, {
      method: 'POST',
      signal,
      headers: {
        'Content-Type': 'application/json',
        'X-Tenant-Id': this.tenantId,
        Authorization: this.token ? `Bearer ${this.token}` : ''
      },
      body: JSON.stringify(req)
    });
    if (!res.ok) {
      const text = await res.text().catch(() => '');
      throw new Error(`dispatch ${res.status}: ${text}`);
    }
    const json = (await res.json()) as { success: boolean; data: DispatchResponse; code?: string; message?: string };
    if (!json.success) throw new Error(`${json.code}: ${json.message}`);
    return json.data;
  }
}

export class MockDispatchClient implements DispatchClient {
  constructor(public handler: (req: DispatchRequest) => Promise<DispatchResponse> | DispatchResponse) {}
  async send(req: DispatchRequest): Promise<DispatchResponse> {
    return this.handler(req);
  }
}

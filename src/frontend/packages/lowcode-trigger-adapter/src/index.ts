/**
 * Trigger 适配器（M12 C12-1）。
 */

export type TriggerKind = 'cron' | 'event' | 'webhook';

export interface TriggerInfo {
  id: string;
  name: string;
  kind: TriggerKind;
  cron?: string;
  eventName?: string;
  webhookSecret?: string;
  enabled: boolean;
  workflowId?: string;
  chatflowId?: string;
  createdAt: string;
  updatedAt: string;
  lastFiredAt?: string;
}

export interface TriggerUpsertRequest {
  id?: string;
  name: string;
  kind: TriggerKind;
  cron?: string;
  eventName?: string;
  workflowId?: string;
  chatflowId?: string;
  enabled?: boolean;
}

export interface TriggerAdapter {
  upsert(req: TriggerUpsertRequest): Promise<{ id: string }>;
  list(): Promise<TriggerInfo[]>;
  delete(id: string): Promise<void>;
  pause(id: string): Promise<void>;
  resume(id: string): Promise<void>;
}

const ROOT = '/api/runtime/triggers';

interface ApiResponse<T> { success: boolean; data: T; code?: string; message?: string }

export interface HttpTriggerAdapterOptions { tenantId: string; token?: string; }

export class HttpTriggerAdapter implements TriggerAdapter {
  constructor(private readonly opts: HttpTriggerAdapterOptions) {}

  upsert(req: TriggerUpsertRequest): Promise<{ id: string }> {
    return this.fetchJson<{ id: string }>(req.id ? `${ROOT}/${encodeURIComponent(req.id)}` : ROOT, req.id ? 'PUT' : 'POST', req);
  }
  list(): Promise<TriggerInfo[]> {
    return this.fetchJson<TriggerInfo[]>(ROOT, 'GET');
  }
  async delete(id: string): Promise<void> {
    await this.fetchJson<unknown>(`${ROOT}/${encodeURIComponent(id)}`, 'DELETE');
  }
  async pause(id: string): Promise<void> {
    await this.fetchJson<unknown>(`${ROOT}/${encodeURIComponent(id)}:pause`, 'POST');
  }
  async resume(id: string): Promise<void> {
    await this.fetchJson<unknown>(`${ROOT}/${encodeURIComponent(id)}:resume`, 'POST');
  }

  private async fetchJson<T>(path: string, method: string, body?: unknown): Promise<T> {
    const res = await fetch(path, {
      method,
      headers: { 'Content-Type': 'application/json', 'X-Tenant-Id': this.opts.tenantId, Authorization: this.opts.token ? `Bearer ${this.opts.token}` : '' },
      body: body ? JSON.stringify(body) : undefined
    });
    if (!res.ok) {
      const text = await res.text().catch(() => '');
      throw new Error(`trigger ${method} ${path} ${res.status}: ${text}`);
    }
    const json = (await res.json()) as ApiResponse<T>;
    if (!json.success) throw new Error(`${json.code ?? 'TRIGGER_ERROR'}: ${json.message ?? 'unknown'}`);
    return json.data;
  }
}

/** 简单 CRON 表达式校验（5 字段或 6 字段；M12 客户端预校验，服务端二次）。*/
const CRON_RE = /^(\S+\s+){4,5}\S+$/;
export function isValidCron(expr: string): boolean {
  return CRON_RE.test(expr.trim());
}

export const __ATLAS_LOWCODE_PACKAGE__ = '@atlas/lowcode-trigger-adapter' as const;

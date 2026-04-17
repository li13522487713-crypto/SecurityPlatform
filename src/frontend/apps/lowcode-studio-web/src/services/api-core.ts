/**
 * Studio API 客户端：仅访问设计态 /api/v1/lowcode/* 端点（PlatformHost）。
 *
 * 强约束（PLAN.md §1.3 #2）：
 * - Studio 内禁止直调任何 /api/runtime/* 运行时端点；运行时调用一律由 lowcode-runtime-web
 *   通过 dispatch（M13）路由。
 * - Studio 调试预览经 lowcode-preview-web (M08) 的 SignalR `/hubs/lowcode-preview`，本客户端不直连。
 */

import type { JsonValue } from '@atlas/lowcode-schema';

const BASE = '/api/v1/lowcode';

interface ApiResponse<T> {
  success: boolean;
  code: string;
  message: string;
  traceId: string;
  data: T;
}

async function request<T>(method: string, path: string, body?: JsonValue): Promise<T> {
  const tenantId = (typeof localStorage !== 'undefined' ? localStorage.getItem('atlas_tenant_id') : null) ?? '00000000-0000-0000-0000-000000000001';
  const token = (typeof localStorage !== 'undefined' ? localStorage.getItem('atlas_access_token') : null) ?? '';
  const res = await fetch(`${BASE}${path}`, {
    method,
    headers: {
      'Content-Type': 'application/json',
      'X-Tenant-Id': tenantId,
      Authorization: token ? `Bearer ${token}` : ''
    },
    body: body ? JSON.stringify(body) : undefined
  });
  if (!res.ok) {
    const text = await res.text().catch(() => '');
    throw new Error(`API ${method} ${path} 失败 ${res.status}: ${text}`);
  }
  const json = (await res.json()) as ApiResponse<T>;
  if (!json.success) throw new Error(`${json.code}: ${json.message}`);
  return json.data;
}

export interface AppListItem {
  id: string;
  code: string;
  displayName: string;
  description?: string;
  schemaVersion: string;
  targetTypes: string;
  defaultLocale: string;
  status: string;
  currentVersionId?: string | number | null;
  createdAt: string;
  updatedAt: string;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  pageIndex: number;
  pageSize: number;
}

export const lowcodeApi = {
  apps: {
    list: (pageIndex = 1, pageSize = 20, keyword?: string, status?: string) => {
      const params = new URLSearchParams({ pageIndex: String(pageIndex), pageSize: String(pageSize) });
      if (keyword) params.set('keyword', keyword);
      if (status) params.set('status', status);
      return request<PagedResult<AppListItem>>('GET', `/apps?${params}`);
    },
    create: (body: { code: string; displayName: string; description?: string; targetTypes: string; defaultLocale?: string }) =>
      request<{ id: string }>('POST', '/apps', body as never),
    detail: (id: string) => request<AppListItem>('GET', `/apps/${id}`),
    delete: (id: string) => request<unknown>('DELETE', `/apps/${id}`),
    getDraft: (id: string) => request<{ schemaJson: string; schemaVersion: string; updatedAt: string }>('GET', `/apps/${id}/draft`),
    replaceDraft: (id: string, schemaJson: string) => request<unknown>('POST', `/apps/${id}/draft`, { schemaJson } as never),
    autosave: (id: string, schemaJson: string) => request<unknown>('POST', `/apps/${id}/autosave`, { schemaJson } as never),
    snapshot: (id: string, versionLabel: string, note?: string) =>
      request<{ versionId: string }>('POST', `/apps/${id}/snapshot`, { versionLabel, note } as never),
    listVersions: (id: string) => request<unknown[]>('GET', `/apps/${id}/versions`),
    getSchema: (id: string) => request<unknown>('GET', `/apps/${id}/schema`)
  },
  draftLock: {
    acquire: (appId: string, sessionId: string) => request<unknown>('POST', `/apps/${appId}/draft-lock/acquire`, { sessionId } as never),
    renew: (appId: string, sessionId: string) => request<unknown>('POST', `/apps/${appId}/draft-lock/renew`, { sessionId } as never),
    release: (appId: string, sessionId: string) => request<unknown>('POST', `/apps/${appId}/draft-lock/release`, { sessionId } as never),
    status: (appId: string) => request<unknown>('GET', `/apps/${appId}/draft-lock/status`)
  },
  components: {
    registry: (renderer = 'web') => request<unknown>('GET', `/components/registry?renderer=${renderer}`)
  }
};

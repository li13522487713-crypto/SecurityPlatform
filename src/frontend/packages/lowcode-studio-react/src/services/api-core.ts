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

export type LowcodeRequest = <T>(method: string, path: string, body?: JsonValue) => Promise<T>;

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

/** 通用资源条目（M07 S07-3）。*/
export interface ResourceItem {
  resourceType: string;
  id: string;
  name: string;
  description?: string;
  updatedAt?: string;
}

export interface ResourceCatalog {
  byType: Record<string, ResourceItem[]>;
  total: number;
}

export interface ComponentMetaWire {
  type: string;
  displayName: string;
  category: string;
  group?: string;
  version: string;
  bindableProps: string[];
  contentParams?: string[];
  supportedEvents: string[];
  childPolicy: { arity: string; allowTypes?: string[] };
  runtimeRenderer: string[];
}

export interface ComponentRegistry {
  components: ComponentMetaWire[];
  overrides: Array<{ type: string; hidden: boolean; defaultPropsJson?: string }>;
}

export interface AppTemplate {
  id: string;
  code: string;
  name: string;
  kind: string;
  description?: string;
  industryTag?: string;
  templateJson: string;
  shareScope: string;
  stars: number;
  useCount: number;
  createdByUserId: string;
  createdAt: string;
  updatedAt: string;
}

export interface AppPage {
  id: string;
  appId: string;
  code: string;
  displayName: string;
  path: string;
  targetType: string;
  layout: string;
  orderNo: number;
  isVisible: boolean;
  isLocked: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface AppVariable {
  id: string;
  appId: string;
  code: string;
  displayName: string;
  scope: string;
  valueType: string;
  isReadOnly: boolean;
  isPersisted: boolean;
  defaultValueJson: string;
  description?: string;
}

export interface VersionDiff {
  fromVersionId: string;
  toVersionId: string;
  fromLabel: string;
  toLabel: string;
  ops: Array<{ op: 'add' | 'remove' | 'replace'; path: string; before?: string; after?: string }>;
}

export interface FaqEntry {
  id: string;
  title: string;
  body: string;
  tags?: string;
  hits: number;
  updatedAt: string;
}

export interface PublishArtifact {
  id: string;
  appId: string;
  versionId: string;
  kind: 'hosted' | 'embedded-sdk' | 'preview';
  status: string;
  fingerprint: string;
  publicUrl?: string;
  rendererMatrixJson: string;
  errorMessage?: string;
  publishedByUserId: string;
  createdAt: string;
  updatedAt: string;
}

export function createLowcodeApi(requestImpl: LowcodeRequest) {
  return {
  apps: {
    list: (pageIndex = 1, pageSize = 20, keyword?: string, status?: string) => {
      const params = new URLSearchParams({ pageIndex: String(pageIndex), pageSize: String(pageSize) });
      if (keyword) params.set('keyword', keyword);
      if (status) params.set('status', status);
      return requestImpl<PagedResult<AppListItem>>('GET', `/apps?${params}`);
    },
    create: (body: { code: string; displayName: string; description?: string; targetTypes: string; defaultLocale?: string }) =>
      requestImpl<{ id: string }>('POST', '/apps', body as never),
    detail: (id: string) => requestImpl<AppListItem>('GET', `/apps/${id}`),
    delete: (id: string) => requestImpl<unknown>('DELETE', `/apps/${id}`),
    getDraft: (id: string) => requestImpl<{ schemaJson: string; schemaVersion: string; updatedAt: string }>('GET', `/apps/${id}/draft`),
    replaceDraft: (id: string, schemaJson: string) => requestImpl<unknown>('POST', `/apps/${id}/draft`, { schemaJson } as never),
    autosave: (id: string, schemaJson: string) => requestImpl<unknown>('POST', `/apps/${id}/autosave`, { schemaJson } as never),
    snapshot: (id: string, versionLabel: string, note?: string) =>
      requestImpl<{ versionId: string }>('POST', `/apps/${id}/snapshot`, { versionLabel, note } as never),
    listVersions: (id: string) => requestImpl<Array<{ id: string; appId: string; versionLabel: string; note?: string; isSystemSnapshot: boolean; createdByUserId: number; createdAt: string }>>('GET', `/apps/${id}/versions`),
    getSchema: (id: string) => requestImpl<unknown>('GET', `/apps/${id}/schema`)
  },
  pages: {
    list: (appId: string) => requestImpl<AppPage[]>('GET', `/apps/${appId}/pages`),
    create: (appId: string, body: { code: string; displayName: string; path: string; targetType?: string; layout?: string }) =>
      requestImpl<{ id: string }>('POST', `/apps/${appId}/pages`, body as never),
    delete: (appId: string, pageId: string) => requestImpl<unknown>('DELETE', `/apps/${appId}/pages/${pageId}`)
  },
  variables: {
    list: (appId: string, scope?: string) => requestImpl<AppVariable[]>('GET', `/apps/${appId}/variables${scope ? `?scope=${scope}` : ''}`),
    create: (appId: string, body: AppVariable & { isReadOnly?: boolean; isPersisted?: boolean }) =>
      requestImpl<{ id: string }>('POST', `/apps/${appId}/variables`, body as never),
    delete: (appId: string, variableId: string) => requestImpl<unknown>('DELETE', `/apps/${appId}/variables/${variableId}`)
  },
  resources: {
    search: (appId: string, params: { types?: string; keyword?: string; pageIndex?: number; pageSize?: number } = {}) => {
      const sp = new URLSearchParams();
      if (params.types) sp.set('types', params.types);
      if (params.keyword) sp.set('keyword', params.keyword);
      sp.set('pageIndex', String(params.pageIndex ?? 1));
      sp.set('pageSize', String(params.pageSize ?? 20));
      return requestImpl<ResourceCatalog>('GET', `/apps/${appId}/resources?${sp}`);
    }
  },
  templates: {
    search: (params: { keyword?: string; kind?: string; shareScope?: string; industryTag?: string; pageIndex?: number; pageSize?: number } = {}) => {
      const sp = new URLSearchParams();
      for (const [k, v] of Object.entries(params)) {
        if (v !== undefined && v !== '') sp.set(k, String(v));
      }
      return requestImpl<AppTemplate[]>('GET', `/templates?${sp}`);
    },
    apply: (id: string) => requestImpl<{ templateId: string; templateJson: string; useCount: number }>('POST', `/templates/${id}/apply`),
    star: (id: string, increment = true) => requestImpl<{ stars: number }>('POST', `/templates/${id}/star?increment=${increment}`)
  },
  faq: {
    search: (keyword?: string, pageIndex = 1, pageSize = 20) => {
      const sp = new URLSearchParams({ pageIndex: String(pageIndex), pageSize: String(pageSize) });
      if (keyword) sp.set('keyword', keyword);
      return requestImpl<FaqEntry[]>('GET', `/faq?${sp}`);
    },
    hit: (id: string) => requestImpl<FaqEntry | null>('POST', `/faq/${id}/hit`)
  },
  versions: {
    diff: (appId: string, fromId: string, toId: string) =>
      requestImpl<VersionDiff>('GET', `/apps/${appId}/versions/${fromId}/diff/${toId}`),
    rollback: (appId: string, versionId: string, note?: string) =>
      requestImpl<unknown>('POST', `/apps/${appId}/versions/${versionId}/rollback`, { note } as never)
  },
  publish: {
    list: (appId: string) => requestImpl<PublishArtifact[]>('GET', `/apps/${appId}/artifacts`),
    publish: (appId: string, kind: 'hosted' | 'embedded-sdk' | 'preview', body?: { versionId?: string; rendererMatrixJson?: string }) =>
      requestImpl<PublishArtifact>('POST', `/apps/${appId}/publish/${kind}`, { kind, ...body } as never),
    rollback: (appId: string, artifactId: string) => requestImpl<unknown>('POST', `/apps/${appId}/publish/rollback`, { artifactId } as never)
  },
  draftLock: {
    acquire: (appId: string, sessionId: string) => requestImpl<unknown>('POST', `/apps/${appId}/draft-lock/acquire`, { sessionId } as never),
    renew: (appId: string, sessionId: string) => requestImpl<unknown>('POST', `/apps/${appId}/draft-lock/renew`, { sessionId } as never),
    release: (appId: string, sessionId: string) => requestImpl<unknown>('POST', `/apps/${appId}/draft-lock/release`, { sessionId } as never),
    status: (appId: string) => requestImpl<unknown>('GET', `/apps/${appId}/draft-lock/status`)
  },
  components: {
    registry: (renderer = 'web') => requestImpl<ComponentRegistry>('GET', `/components/registry?renderer=${renderer}`)
  }
  };
}

export type LowcodeApi = ReturnType<typeof createLowcodeApi>;

export const lowcodeApi = createLowcodeApi(request);

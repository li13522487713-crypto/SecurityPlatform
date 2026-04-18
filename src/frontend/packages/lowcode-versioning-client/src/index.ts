/**
 * Atlas 低代码版本管理客户端（M14 C14-1..C14-6）。
 *
 * - VersioningClient：list / diff / rollback / archive（runtime）/ 资源引用反查 / FAQ 检索
 * - groupDiffsByGroup：把扁平 ops 分组（基础信息 / 页面 / 变量 / 内容参数 / 其它）便于 UI 展开
 */

export interface VersionListItem {
  id: string;
  appId: string;
  versionLabel: string;
  note?: string;
  isSystemSnapshot: boolean;
  createdByUserId: number;
  createdAt: string;
}

export interface DiffOp { op: 'add' | 'remove' | 'replace'; path: string; before?: string; after?: string }

export interface VersionDiff {
  fromVersionId: string;
  toVersionId: string;
  fromLabel: string;
  toLabel: string;
  ops: DiffOp[];
}

export interface ResourceReference {
  id: string;
  appId: string;
  pageId?: string;
  componentId?: string;
  resourceType: string;
  resourceId: string;
  referencePath: string;
  resourceVersion?: string;
  createdAt: string;
}

export interface FaqEntry {
  id: string;
  title: string;
  body: string;
  tags?: string;
  hits: number;
  updatedAt: string;
}

interface ApiResponse<T> { success: boolean; data: T; code?: string; message?: string }

export interface VersioningClientOptions { tenantId: string; token?: string }

export class VersioningClient {
  constructor(private readonly opts: VersioningClientOptions) {}

  listVersions(appId: string, includeSystemSnapshot = false): Promise<VersionListItem[]> {
    return this.fetchJson<VersionListItem[]>(`/api/v1/lowcode/apps/${encodeURIComponent(appId)}/versions?includeSystemSnapshot=${includeSystemSnapshot}`, 'GET');
  }
  diff(appId: string, fromId: string, toId: string): Promise<VersionDiff> {
    return this.fetchJson<VersionDiff>(`/api/v1/lowcode/apps/${encodeURIComponent(appId)}/versions/${encodeURIComponent(fromId)}/diff/${encodeURIComponent(toId)}`, 'GET');
  }
  async rollbackDesign(appId: string, versionId: string, note?: string): Promise<void> {
    await this.fetchJson<unknown>(`/api/v1/lowcode/apps/${encodeURIComponent(appId)}/versions/${encodeURIComponent(versionId)}/rollback`, 'POST', { note });
  }
  archiveRuntime(appId: string): Promise<{ versionId: string }> {
    return this.fetchJson<{ versionId: string }>(`/api/runtime/versions/archive`, 'POST', { appId });
  }
  async rollbackRuntime(appId: string, versionId: string): Promise<void> {
    await this.fetchJson<unknown>(`/api/runtime/versions/${encodeURIComponent(versionId)}:rollback`, 'POST', { appId });
  }
  listReferences(resourceType: string, resourceId: string): Promise<ResourceReference[]> {
    return this.fetchJson<ResourceReference[]>(`/api/v1/lowcode/resources/${encodeURIComponent(resourceType)}/${encodeURIComponent(resourceId)}/references`, 'GET');
  }
  searchFaq(keyword?: string, pageIndex = 1, pageSize = 20): Promise<FaqEntry[]> {
    const q = `?keyword=${encodeURIComponent(keyword ?? '')}&pageIndex=${pageIndex}&pageSize=${pageSize}`;
    return this.fetchJson<FaqEntry[]>(`/api/v1/lowcode/faq${q}`, 'GET');
  }

  private async fetchJson<T>(path: string, method: string, body?: unknown): Promise<T> {
    const res = await fetch(path, {
      method,
      headers: { 'Content-Type': 'application/json', 'X-Tenant-Id': this.opts.tenantId, Authorization: this.opts.token ? `Bearer ${this.opts.token}` : '' },
      body: body ? JSON.stringify(body) : undefined
    });
    if (!res.ok) {
      const text = await res.text().catch(() => '');
      throw new Error(`versioning ${method} ${path} ${res.status}: ${text}`);
    }
    const json = (await res.json()) as ApiResponse<T>;
    if (!json.success) throw new Error(`${json.code ?? 'VERSIONING_ERROR'}: ${json.message ?? 'unknown'}`);
    return json.data;
  }
}

/** 把 diff ops 按 path 顶层段分组，便于 UI 折叠展开。*/
export function groupDiffsByGroup(ops: ReadonlyArray<DiffOp>): Record<string, DiffOp[]> {
  const map: Record<string, DiffOp[]> = {};
  for (const op of ops) {
    const seg = (op.path.replace(/^\//, '').split('/')[0]) || '_root';
    (map[seg] ??= []).push(op);
  }
  return map;
}

export const __ATLAS_LOWCODE_PACKAGE__ = '@atlas/lowcode-versioning-client' as const;

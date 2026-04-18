/**
 * Webview 域名白名单适配器（M12 C12-2 + M17 C17-8）。
 */

export type DomainVerificationKind = 'dns_txt' | 'http_file';

export interface WebviewDomainInfo {
  id: string;
  domain: string;
  verified: boolean;
  verificationKind?: DomainVerificationKind;
  /** 服务端颁发的随机校验串（DNS TXT / 文件名前缀）。*/
  verificationToken: string;
  createdAt: string;
  verifiedAt?: string;
}

export interface AddDomainRequest {
  domain: string;
  verificationKind: DomainVerificationKind;
}

export interface WebviewPolicyAdapter {
  addDomain(req: AddDomainRequest): Promise<WebviewDomainInfo>;
  verifyDomain(id: string): Promise<WebviewDomainInfo>;
  listDomains(): Promise<WebviewDomainInfo[]>;
  removeDomain(id: string): Promise<void>;
  /** 运行时检查 url 是否被白名单允许。*/
  isAllowed(url: string): boolean;
}

const ROOT = '/api/runtime/webview-domains';

interface ApiResponse<T> { success: boolean; data: T; code?: string; message?: string }

export interface HttpWebviewPolicyAdapterOptions {
  tenantId: string;
  token?: string;
  /** 缓存的白名单域名（运行时同步刷新）。*/
  initialDomains?: string[];
}

export class HttpWebviewPolicyAdapter implements WebviewPolicyAdapter {
  private cache: Set<string>;

  constructor(private readonly opts: HttpWebviewPolicyAdapterOptions) {
    this.cache = new Set((opts.initialDomains ?? []).map((d) => d.toLowerCase()));
  }

  async addDomain(req: AddDomainRequest): Promise<WebviewDomainInfo> {
    const r = await this.fetchJson<WebviewDomainInfo>(ROOT, 'POST', req);
    if (r.verified) this.cache.add(r.domain.toLowerCase());
    return r;
  }

  async verifyDomain(id: string): Promise<WebviewDomainInfo> {
    const r = await this.fetchJson<WebviewDomainInfo>(`${ROOT}/${encodeURIComponent(id)}:verify`, 'POST');
    if (r.verified) this.cache.add(r.domain.toLowerCase());
    return r;
  }

  async listDomains(): Promise<WebviewDomainInfo[]> {
    const list = await this.fetchJson<WebviewDomainInfo[]>(ROOT, 'GET');
    this.cache = new Set(list.filter((d) => d.verified).map((d) => d.domain.toLowerCase()));
    return list;
  }

  async removeDomain(id: string): Promise<void> {
    const list = await this.listDomains();
    const target = list.find((d) => d.id === id);
    await this.fetchJson<unknown>(`${ROOT}/${encodeURIComponent(id)}`, 'DELETE');
    if (target) this.cache.delete(target.domain.toLowerCase());
  }

  isAllowed(url: string): boolean {
    try {
      const u = new URL(url);
      const host = u.hostname.toLowerCase();
      if (this.cache.has(host)) return true;
      // 通配符支持：*.example.com 形式存储为 .example.com
      for (const d of this.cache) {
        if (d.startsWith('.') && host.endsWith(d)) return true;
      }
      return false;
    } catch {
      return false;
    }
  }

  private async fetchJson<T>(path: string, method: string, body?: unknown): Promise<T> {
    const res = await fetch(path, {
      method,
      headers: { 'Content-Type': 'application/json', 'X-Tenant-Id': this.opts.tenantId, Authorization: this.opts.token ? `Bearer ${this.opts.token}` : '' },
      body: body ? JSON.stringify(body) : undefined
    });
    if (!res.ok) {
      const text = await res.text().catch(() => '');
      throw new Error(`webview ${method} ${path} ${res.status}: ${text}`);
    }
    const json = (await res.json()) as ApiResponse<T>;
    if (!json.success) throw new Error(`${json.code ?? 'WEBVIEW_ERROR'}: ${json.message ?? 'unknown'}`);
    return json.data;
  }
}

/** 单纯协议级 isAllowed：用于无 fetch 上下文的纯函数判定（如设计期 Studio 内）。*/
export function isUrlAllowed(url: string, whitelist: ReadonlyArray<string>): boolean {
  try {
    const u = new URL(url);
    const host = u.hostname.toLowerCase();
    return whitelist.some((d) => {
      const k = d.toLowerCase();
      if (k.startsWith('.')) {
        const bare = k.slice(1);
        return host === bare || host.endsWith(k);
      }
      return host === k;
    });
  } catch {
    return false;
  }
}

export const __ATLAS_LOWCODE_PACKAGE__ = '@atlas/lowcode-webview-policy-adapter' as const;

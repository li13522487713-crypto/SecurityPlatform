/**
 * @atlas/lowcode-plugin-adapter（M18 C18-7）。
 * 设计态：/api/v1/lowcode/plugins  + /prompt-templates
 * 运行时：/api/runtime/plugins/{id}:invoke
 */

export interface PluginDefinition {
  id: string;
  pluginId: string;
  name: string;
  description?: string;
  toolsJson: string;
  latestVersion: string;
  shareScope: 'private' | 'team' | 'public';
  createdByUserId: string;
  createdAt: string;
  updatedAt: string;
}

export interface PluginInvokeResult {
  pluginId: string;
  toolName: string;
  status: 'success' | 'failed';
  outputs?: Record<string, unknown>;
  errorMessage?: string;
}

export interface PromptTemplate {
  id: string;
  code: string;
  name: string;
  body: string;
  mode: 'jinja' | 'markdown' | 'plain';
  version: string;
  description?: string;
  shareScope: 'private' | 'team' | 'public';
  createdByUserId: string;
  createdAt: string;
  updatedAt: string;
}

interface ApiResponse<T> { success: boolean; data: T; code?: string; message?: string }

export interface PluginAdapterOptions { tenantId: string; token?: string }

export class PluginAdapter {
  constructor(private readonly opts: PluginAdapterOptions) {}

  searchPlugins(keyword?: string, shareScope?: 'private' | 'team' | 'public'): Promise<PluginDefinition[]> {
    const q = `?keyword=${encodeURIComponent(keyword ?? '')}&shareScope=${shareScope ?? ''}`;
    return this.fetchJson<PluginDefinition[]>(`/api/v1/lowcode/plugins${q}`, 'GET');
  }
  upsertPlugin(body: { id?: string; name: string; description?: string; toolsJson: string; shareScope?: string }): Promise<{ id: string }> {
    return this.fetchJson<{ id: string }>('/api/v1/lowcode/plugins', 'POST', body);
  }
  publishPluginVersion(defId: string, version: string): Promise<{ versionId: string }> {
    return this.fetchJson<{ versionId: string }>(`/api/v1/lowcode/plugins/${encodeURIComponent(defId)}/publish`, 'POST', { version });
  }
  authorize(pluginId: string, authKind: 'api_key' | 'oauth' | 'basic' | 'none', credential?: string): Promise<{ id: string }> {
    return this.fetchJson<{ id: string }>(`/api/v1/lowcode/plugins/${encodeURIComponent(pluginId)}/authorize`, 'POST', { authKind, credential });
  }
  invoke(pluginId: string, toolName: string, args?: Record<string, unknown>): Promise<PluginInvokeResult> {
    return this.fetchJson<PluginInvokeResult>(`/api/runtime/plugins/${encodeURIComponent(pluginId)}:invoke`, 'POST', { pluginId, toolName, args });
  }

  searchPromptTemplates(keyword?: string): Promise<PromptTemplate[]> {
    return this.fetchJson<PromptTemplate[]>(`/api/v1/lowcode/prompt-templates?keyword=${encodeURIComponent(keyword ?? '')}`, 'GET');
  }
  upsertPromptTemplate(body: { id?: string; code: string; name: string; body: string; mode?: string; description?: string; shareScope?: string }): Promise<{ id: string }> {
    return this.fetchJson<{ id: string }>('/api/v1/lowcode/prompt-templates', 'POST', body);
  }

  private async fetchJson<T>(path: string, method: string, body?: unknown): Promise<T> {
    const res = await fetch(path, {
      method,
      headers: { 'Content-Type': 'application/json', 'X-Tenant-Id': this.opts.tenantId, Authorization: this.opts.token ? `Bearer ${this.opts.token}` : '' },
      body: body ? JSON.stringify(body) : undefined
    });
    if (!res.ok) {
      const text = await res.text().catch(() => '');
      throw new Error(`plugin ${method} ${path} ${res.status}: ${text}`);
    }
    const json = (await res.json()) as ApiResponse<T>;
    if (!json.success) throw new Error(`${json.code ?? 'PLUGIN_ERROR'}: ${json.message ?? 'unknown'}`);
    return json.data;
  }
}

export const __ATLAS_LOWCODE_PACKAGE__ = '@atlas/lowcode-plugin-adapter' as const;

/**
 * HttpAssetAdapter（M10 C10-1）：基于 fetch 调用 RuntimeFilesController（AppHost /api/runtime/files）。
 */

import {
  ensureMimeAllowed,
  ensureSizeAllowed,
  type AssetAdapter,
  type CompleteUploadResponse,
  type PrepareUploadRequest,
  type PrepareUploadResponse
} from './types';

interface ApiResponse<T> {
  success: boolean;
  code?: string;
  message?: string;
  data: T;
}

const ROOT = '/api/runtime/files';

export interface HttpAssetAdapterOptions {
  tenantId: string;
  token?: string;
  /** 自定义大小上限。*/
  maxSizeBytes?: number;
}

export class HttpAssetAdapter implements AssetAdapter {
  constructor(private readonly opts: HttpAssetAdapterOptions) {}

  async prepareUpload(req: PrepareUploadRequest, signal?: AbortSignal): Promise<PrepareUploadResponse> {
    ensureMimeAllowed(req.contentType);
    ensureSizeAllowed(req.size, this.opts.maxSizeBytes);
    return this.fetchJson<PrepareUploadResponse>(`${ROOT}:prepare-upload`, 'POST', req, signal);
  }

  async completeUpload(
    token: string,
    blob: Blob | File,
    signal?: AbortSignal,
    onProgress?: (p: { loaded: number; total: number }) => void
  ): Promise<CompleteUploadResponse> {
    // 简单实现：使用 fetch 一次性 PUT；进度回调用 ReadableStream tee 即可，但浏览器原生 fetch 不暴露上传进度，
    // 这里给出一个 noop 进度（单次完成）；细粒度进度由 XHR 路径或 Tus 路径在 M10 增强。
    onProgress?.({ loaded: 0, total: blob.size });
    const formData = new FormData();
    formData.append('token', token);
    formData.append('file', blob);
    const res = await fetch(`${ROOT}:complete-upload`, {
      method: 'POST',
      signal,
      headers: {
        'X-Tenant-Id': this.opts.tenantId,
        Authorization: this.opts.token ? `Bearer ${this.opts.token}` : ''
      },
      body: formData
    });
    if (!res.ok) {
      const text = await res.text().catch(() => '');
      throw new Error(`asset complete-upload ${res.status}: ${text}`);
    }
    const json = (await res.json()) as ApiResponse<CompleteUploadResponse>;
    if (!json.success) throw new Error(`${json.code}: ${json.message}`);
    onProgress?.({ loaded: blob.size, total: blob.size });
    return json.data;
  }

  async cancel(token: string): Promise<void> {
    await this.fetchJson<unknown>(`${ROOT}/sessions/${encodeURIComponent(token)}:cancel`, 'POST');
  }

  async remove(fileHandle: string): Promise<void> {
    await this.fetchJson<unknown>(`${ROOT}/${encodeURIComponent(fileHandle)}`, 'DELETE');
  }

  private async fetchJson<T>(path: string, method: string, body?: unknown, signal?: AbortSignal): Promise<T> {
    const res = await fetch(path, {
      method,
      signal,
      headers: {
        'Content-Type': 'application/json',
        'X-Tenant-Id': this.opts.tenantId,
        Authorization: this.opts.token ? `Bearer ${this.opts.token}` : ''
      },
      body: body ? JSON.stringify(body) : undefined
    });
    if (!res.ok) {
      const text = await res.text().catch(() => '');
      throw new Error(`asset ${method} ${path} ${res.status}: ${text}`);
    }
    const json = (await res.json()) as ApiResponse<T>;
    if (!json.success) throw new Error(`${json.code ?? 'ASSET_ERROR'}: ${json.message ?? 'unknown'}`);
    return json.data;
  }
}

/**
 * HttpWorkflowAdapter（M09 C09-1）：基于 fetch 调用 RuntimeWorkflowsController（AppHost /api/runtime/workflows）。
 *
 * 强约束（PLAN.md §1.3 #2）：
 * - 仅由 dispatch 控制器内部 / lowcode-runtime-web 内部使用；
 * - UI 直调路径在 lowcode-runtime-web 已经守门；本适配器再次明确：禁止从 apps/* 直接 import。
 */

import { withResilience } from '@atlas/lowcode-action-runtime/resilience';
import type { ResiliencePolicy } from '@atlas/lowcode-schema';
import type {
  WorkflowAdapter,
  WorkflowAsyncJob,
  WorkflowBatchInvokeRequest,
  WorkflowBatchResult,
  WorkflowInvokeRequest,
  WorkflowInvokeResult
} from './types';
import { DEFAULT_WORKFLOW_RESILIENCE, mergeResiliencePolicy } from './resilience';

interface ApiResponse<T> {
  success: boolean;
  code?: string;
  message?: string;
  data: T;
  traceId?: string;
}

const ROOT = '/api/runtime/workflows';
const ASYNC_ROOT = '/api/runtime/async-jobs';

export interface HttpAdapterOptions {
  tenantId: string;
  token?: string;
  /** 默认弹性策略；可被单次调用 override 覆盖。*/
  defaultPolicy?: ResiliencePolicy;
  /** 失败时的兜底 workflow id（DEFAULT 全局降级）。*/
  defaultFallbackWorkflowId?: string;
}

export class HttpWorkflowAdapter implements WorkflowAdapter {
  constructor(private readonly opts: HttpAdapterOptions) {}

  async invoke(req: WorkflowInvokeRequest, signal?: AbortSignal): Promise<WorkflowInvokeResult> {
    const policy = mergeResiliencePolicy(this.opts.defaultPolicy ?? DEFAULT_WORKFLOW_RESILIENCE);
    const op = () => this.doInvoke(req, signal);
    const fallback = this.opts.defaultFallbackWorkflowId
      ? () => this.doInvoke({ ...req, workflowId: this.opts.defaultFallbackWorkflowId! }, signal)
      : undefined;
    return withResilience(op, {
      policy,
      circuitKey: `workflow:${req.workflowId}`,
      fallback
    });
  }

  async invokeAsync(req: WorkflowInvokeRequest): Promise<{ jobId: string }> {
    return this.fetchJson<{ jobId: string }>(`${ROOT}/${encodeURIComponent(req.workflowId)}:invoke-async`, 'POST', req);
  }

  async getAsyncJob(jobId: string): Promise<WorkflowAsyncJob> {
    return this.fetchJson<WorkflowAsyncJob>(`${ASYNC_ROOT}/${encodeURIComponent(jobId)}`, 'GET');
  }

  async cancelAsyncJob(jobId: string): Promise<void> {
    await this.fetchJson<unknown>(`${ASYNC_ROOT}/${encodeURIComponent(jobId)}:cancel`, 'POST');
  }

  async invokeBatch(req: WorkflowBatchInvokeRequest): Promise<WorkflowBatchResult> {
    return this.fetchJson<WorkflowBatchResult>(`${ROOT}/${encodeURIComponent(req.workflowId)}:invoke-batch`, 'POST', req);
  }

  private async doInvoke(req: WorkflowInvokeRequest, signal?: AbortSignal): Promise<WorkflowInvokeResult> {
    return this.fetchJson<WorkflowInvokeResult>(`${ROOT}/${encodeURIComponent(req.workflowId)}:invoke`, 'POST', req, signal);
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
      throw new Error(`workflow ${method} ${path} ${res.status}: ${text}`);
    }
    const json = (await res.json()) as ApiResponse<T>;
    if (!json.success) throw new Error(`${json.code ?? 'WF_ERROR'}: ${json.message ?? 'unknown'}`);
    return json.data;
  }
}

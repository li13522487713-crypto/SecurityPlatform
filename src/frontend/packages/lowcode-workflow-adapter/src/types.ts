/**
 * Workflow 适配器协议（M09 C09-1）。
 *
 * 严格遵守 PLAN.md §1.3 #2 的"标准化协议唯一桥梁"约束：
 * - 适配器仅提供给 dispatch 控制器内部使用；UI 不允许直接 import 调用。
 * - 真实网络层 invoke 通过 dispatch / 直连 RuntimeWorkflowsController 三入口（同步 / 异步 / 批量）。
 *
 * 详见 docs/lowcode-binding-matrix.md 与 docs/lowcode-resilience-spec.md。
 */

import type { JsonValue, RuntimeStatePatch } from '@atlas/lowcode-schema';

export interface WorkflowInvokeRequest {
  workflowId: string;
  inputs?: Record<string, JsonValue>;
  /** 可选：app/page/version 上下文。*/
  appId?: string;
  pageId?: string;
  versionId?: string;
  /** 可选：触发本次调用的组件实例。*/
  componentId?: string;
}

export interface WorkflowInvokeResult {
  /** Coze 上游 executionId / Atlas DagWorkflow executionId（统一为字符串）。*/
  executionId: string;
  status: 'success' | 'failed' | 'running' | 'cancelled';
  /** 完整 outputs（按工作流 outputSchema 输出）。*/
  outputs?: Record<string, JsonValue>;
  /** dispatch 路径产生的状态补丁（已经过 outputMapping 转换）。*/
  patches?: RuntimeStatePatch[];
  /** 错误信息（status=failed 时）。*/
  errorMessage?: string;
  /** trace id（与 M13 RuntimeTrace 关联）。*/
  traceId?: string;
}

export interface WorkflowAsyncJob {
  jobId: string;
  status: 'pending' | 'running' | 'success' | 'failed' | 'cancelled';
  /** 提交时间。*/
  submittedAt: string;
  /** 完成时间（status 终态时）。*/
  completedAt?: string;
  /** 进度百分比（0-100）。*/
  progressPercent?: number;
  /** 终态结果（status in success/failed 时）。*/
  result?: WorkflowInvokeResult;
}

export interface WorkflowBatchInvokeRequest {
  workflowId: string;
  /** 批输入：每行一份 inputs。*/
  rows: Array<Record<string, JsonValue>>;
  /** 可选：失败行的策略（continue 继续 / abort 立即中止）。*/
  onFailure?: 'continue' | 'abort';
  appId?: string;
  pageId?: string;
}

export interface WorkflowBatchResult {
  jobId: string;
  total: number;
  /** 已完成行数（含成功 + 失败）。*/
  completed: number;
  succeeded: number;
  failed: number;
  /** 详细行结果（jobId 仅含部分 / 全量取决于后端策略）。*/
  rows?: Array<{ index: number; status: 'success' | 'failed'; outputs?: Record<string, JsonValue>; errorMessage?: string }>;
}

export interface WorkflowAdapter {
  /** 同步调用：等待结果。*/
  invoke(req: WorkflowInvokeRequest, signal?: AbortSignal): Promise<WorkflowInvokeResult>;
  /** 异步调用：返回 jobId，由调用方轮询状态。*/
  invokeAsync(req: WorkflowInvokeRequest): Promise<{ jobId: string }>;
  /** 查询异步任务。*/
  getAsyncJob(jobId: string): Promise<WorkflowAsyncJob>;
  /** 取消异步任务。*/
  cancelAsyncJob(jobId: string): Promise<void>;
  /** 批量调用：CSV / JSON / 数据库查询入口由调用方先转 rows 再调用。*/
  invokeBatch(req: WorkflowBatchInvokeRequest): Promise<WorkflowBatchResult>;
}

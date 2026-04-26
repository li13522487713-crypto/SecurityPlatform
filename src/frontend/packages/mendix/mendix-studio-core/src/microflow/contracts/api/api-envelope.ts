import type { MicroflowValidationIssue } from "@atlas/microflow";

import type { MicroflowApiErrorCode } from "./api-error-codes";

/**
 * 统一字段级错误，用于表单/路径绑定。
 */
export interface MicroflowApiFieldError {
  fieldPath: string;
  code: string;
  message: string;
}

/**
 * 与 REST 一致的统一错误体；HTTP 层通常搭配非 2xx 状态码，但 `success:false` 亦可在 200 中携带（不建议）。
 */
export interface MicroflowApiError {
  code: MicroflowApiErrorCode;
  message: string;
  details?: string;
  fieldErrors?: MicroflowApiFieldError[];
  /** 与 ProblemPanel 对齐的服务端校验结果。 */
  validationIssues?: MicroflowValidationIssue[];
  /** 可重试（限流/临时存储失败等）。 */
  retryable?: boolean;
}

/**
 * 统一 API 响应 Envelope。真实 HTTP 客户端在收到 JSON 后先解本结构再交业务层。
 * UI 与 ResourceAdapter/RuntimeAdapter 仍直接消费业务 DTO，不强制经过 Envelope。
 */
export interface MicroflowApiResponse<T> {
  success: boolean;
  data?: T;
  error?: MicroflowApiError;
  traceId?: string;
  timestamp: string;
}

/**
 * 分页结果；`pageIndex` 与 REST 约定为 **1-based**（第 1 页）。前端内部可在 Adapter 中做 0-based 转换。
 */
export interface MicroflowApiPageResult<T> {
  items: T[];
  total: number;
  pageIndex: number;
  pageSize: number;
  hasMore: boolean;
}

/**
 * 随请求通过 Header / JWT / Query 透传的横切身份与租户信息（与存储模型审计字段配合）。
 */
export interface MicroflowApiRequestContext {
  workspaceId?: string;
  tenantId?: string;
  userId?: string;
  locale?: string;
}

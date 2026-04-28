/**
 * Integration/production adapter.
 * Uses backend API contracts and MicroflowApiResponse envelope.
 */
import type { MicroflowApiError } from "../../contracts/api/api-envelope";
import type { MicroflowApiErrorCode } from "../../contracts/api/api-error-codes";

export function mapHttpStatusToMicroflowErrorCode(status?: number): MicroflowApiErrorCode {
  if (status === 401) {
    return "MICROFLOW_UNAUTHORIZED";
  }
  if (status === 403) {
    return "MICROFLOW_PERMISSION_DENIED";
  }
  if (status === 404) {
    return "MICROFLOW_NOT_FOUND";
  }
  if (status === 409) {
    return "MICROFLOW_VERSION_CONFLICT";
  }
  if (status === 422) {
    return "MICROFLOW_VALIDATION_FAILED";
  }
  if (status === 400) {
    return "MICROFLOW_SCHEMA_INVALID";
  }
  if (status && status >= 500) {
    return "MICROFLOW_SERVICE_UNAVAILABLE";
  }
  return "MICROFLOW_UNKNOWN_ERROR";
}

export class MicroflowApiException extends Error {
  readonly status?: number;
  readonly traceId?: string;
  readonly apiError: MicroflowApiError;

  constructor(message: string, options: { status?: number; traceId?: string; apiError?: MicroflowApiError } = {}) {
    super(message);
    this.name = "MicroflowApiException";
    this.status = options.status;
    this.traceId = options.traceId ?? options.apiError?.traceId;
    this.apiError = options.apiError ?? {
      code: mapHttpStatusToMicroflowErrorCode(options.status),
      message,
      httpStatus: options.status,
      traceId: options.traceId,
    };
  }
}

export class MicroflowApiClientError extends MicroflowApiException {}

export function normalizeMicroflowApiError(input: unknown, status?: number, traceId?: string): MicroflowApiError {
  if (isMicroflowApiException(input)) {
    return input.apiError;
  }
  if (typeof input === "object" && input !== null && "code" in input && "message" in input) {
    const error = input as Partial<MicroflowApiError>;
    return {
      code: error.code ?? mapHttpStatusToMicroflowErrorCode(status),
      message: error.message ?? "微流服务异常。",
      details: error.details,
      fieldErrors: error.fieldErrors,
      validationIssues: error.validationIssues,
      retryable: error.retryable,
      httpStatus: error.httpStatus ?? status,
      traceId: error.traceId ?? traceId,
      raw: error.raw,
    };
  }
  if (input instanceof DOMException && input.name === "AbortError") {
    return { code: "MICROFLOW_TIMEOUT", message: "微流请求已取消或超时。", retryable: true, httpStatus: status, traceId, raw: input };
  }
  if (input instanceof TypeError) {
    return { code: "MICROFLOW_NETWORK_ERROR", message: "微流服务不可用，请检查网络或后端服务。", retryable: true, httpStatus: status, traceId, raw: input };
  }
  if (input instanceof Error) {
    return { code: mapHttpStatusToMicroflowErrorCode(status), message: input.message, retryable: status ? status >= 500 : false, httpStatus: status, traceId, raw: input };
  }
  return {
    code: mapHttpStatusToMicroflowErrorCode(status),
    message: typeof input === "string" && input ? input : "微流服务异常。",
    retryable: status ? status >= 500 : false,
    httpStatus: status,
    traceId,
    raw: input,
  };
}

export function createMicroflowApiError(message: string, status?: number): MicroflowApiException {
  return new MicroflowApiException(message, { status });
}

export function isMicroflowApiException(error: unknown): error is MicroflowApiException {
  return error instanceof MicroflowApiException;
}

export function isMicroflowApiClientError(error: unknown): error is MicroflowApiException {
  return isMicroflowApiException(error);
}

export function getMicroflowApiError(error: unknown): MicroflowApiError {
  return normalizeMicroflowApiError(error);
}

export function isUnauthorizedError(error: unknown): boolean {
  return getMicroflowApiError(error).code === "MICROFLOW_UNAUTHORIZED";
}

export function isForbiddenError(error: unknown): boolean {
  return getMicroflowApiError(error).code === "MICROFLOW_PERMISSION_DENIED";
}

export function isNotFoundError(error: unknown): boolean {
  return getMicroflowApiError(error).code === "MICROFLOW_NOT_FOUND";
}

export function isVersionConflictError(error: unknown): boolean {
  return getMicroflowApiError(error).code === "MICROFLOW_VERSION_CONFLICT";
}

export function isValidationFailedError(error: unknown): boolean {
  const apiError = getMicroflowApiError(error);
  return apiError.code === "MICROFLOW_VALIDATION_FAILED" || apiError.code === "MICROFLOW_SCHEMA_INVALID";
}

export function isPublishBlockedError(error: unknown): boolean {
  return getMicroflowApiError(error).code === "MICROFLOW_PUBLISH_BLOCKED";
}

export function isMetadataLoadError(error: unknown): boolean {
  const code = getMicroflowApiError(error).code;
  return code === "MICROFLOW_METADATA_LOAD_FAILED" || code === "MICROFLOW_METADATA_NOT_FOUND";
}

export function isNetworkError(error: unknown): boolean {
  const code = getMicroflowApiError(error).code;
  return code === "MICROFLOW_NETWORK_ERROR" || code === "MICROFLOW_TIMEOUT" || code === "MICROFLOW_SERVICE_UNAVAILABLE";
}

export function isRetryableMicroflowError(error: unknown): boolean {
  const apiError = getMicroflowApiError(error);
  return apiError.retryable === true || isNetworkError(apiError);
}

export function getMicroflowErrorUserMessage(error: unknown): string {
  const apiError = getMicroflowApiError(error);
  switch (apiError.code) {
    case "MICROFLOW_UNAUTHORIZED":
      return "登录已失效，请重新登录。";
    case "MICROFLOW_PERMISSION_DENIED":
      return "当前账号无权限创建微流。";
    case "MICROFLOW_NAME_DUPLICATED":
      return "同名微流已存在。";
    case "MICROFLOW_NOT_FOUND":
      return "微流资源不存在或已被删除。";
    case "MICROFLOW_VERSION_CONFLICT":
      return "微流版本已变化，请刷新后再处理。";
    case "MICROFLOW_VALIDATION_FAILED":
    case "MICROFLOW_SCHEMA_INVALID":
      return "微流校验未通过，请查看问题面板。";
    case "MICROFLOW_PUBLISH_BLOCKED":
      return "微流发布被阻止，请处理校验或影响分析问题。";
    case "MICROFLOW_REFERENCE_BLOCKED":
      return "微流仍被引用，删除或归档已被阻止。";
    case "MICROFLOW_METADATA_LOAD_FAILED":
    case "MICROFLOW_METADATA_NOT_FOUND":
      return "元数据服务不可用，请稍后重试。";
    case "MICROFLOW_NETWORK_ERROR":
    case "MICROFLOW_TIMEOUT":
    case "MICROFLOW_SERVICE_UNAVAILABLE":
      return "微流服务不可用，请检查后端服务或网络。";
    default:
      return apiError.message || "微流服务异常。";
  }
}

export function getMicroflowErrorActionHint(error: unknown): string {
  const apiError = getMicroflowApiError(error);
  if (isRetryableMicroflowError(apiError)) {
    return "请重试，若仍失败请检查 apiBaseUrl 或后端服务状态。";
  }
  if (apiError.code === "MICROFLOW_VERSION_CONFLICT") {
    return "当前未保存内容会保留，请刷新远端版本后再决定是否覆盖。";
  }
  if (apiError.code === "MICROFLOW_NAME_DUPLICATED") {
    return "请使用新的微流名称后重试。";
  }
  if (apiError.code === "MICROFLOW_UNAUTHORIZED") {
    return "请重新登录后再继续。";
  }
  if (apiError.code === "MICROFLOW_PERMISSION_DENIED") {
    return "请联系管理员授予对应工作区权限。";
  }
  return apiError.details ?? "";
}

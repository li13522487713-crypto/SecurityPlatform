import type { MicroflowValidationIssue } from "@atlas/microflow";

import type { MicroflowApiError, MicroflowApiResponse } from "../api/api-envelope";

function traceId(): string {
  return `mock-trace-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
}

export function ok<T>(data: T): MicroflowApiResponse<T> {
  return withTraceId({
    success: true,
    data,
    timestamp: new Date().toISOString(),
  });
}

export function fail(error: MicroflowApiError): MicroflowApiResponse<never> {
  return withTraceId({
    success: false,
    error,
    timestamp: new Date().toISOString(),
  });
}

export function withTraceId<T>(response: MicroflowApiResponse<T>): MicroflowApiResponse<T> {
  const nextTraceId = response.traceId ?? response.error?.traceId ?? traceId();
  return {
    ...response,
    traceId: nextTraceId,
    error: response.error ? { ...response.error, traceId: nextTraceId } : undefined,
  };
}

export function unauthorized(message = "Microflow mock request is unauthorized."): MicroflowApiResponse<never> {
  return fail({ code: "MICROFLOW_UNAUTHORIZED", message, httpStatus: 401 });
}

export function forbidden(message = "Microflow mock request is forbidden."): MicroflowApiResponse<never> {
  return fail({ code: "MICROFLOW_PERMISSION_DENIED", message, httpStatus: 403 });
}

export function notFound(message = "Microflow resource was not found."): MicroflowApiResponse<never> {
  return fail({ code: "MICROFLOW_NOT_FOUND", message, httpStatus: 404 });
}

export function metadataNotFound(message = "Microflow metadata was not found."): MicroflowApiResponse<never> {
  return fail({ code: "MICROFLOW_METADATA_NOT_FOUND", message, httpStatus: 404 });
}

export function versionConflict(message = "Microflow version conflict."): MicroflowApiResponse<never> {
  return fail({ code: "MICROFLOW_VERSION_CONFLICT", message, httpStatus: 409 });
}

export function validationFailed(issues: MicroflowValidationIssue[], message = "Microflow validation failed."): MicroflowApiResponse<never> {
  return fail({
    code: "MICROFLOW_VALIDATION_FAILED",
    message,
    validationIssues: issues,
    httpStatus: 422,
  });
}

export function schemaInvalid(message = "Microflow schema is invalid."): MicroflowApiResponse<never> {
  return fail({ code: "MICROFLOW_SCHEMA_INVALID", message, httpStatus: 422 });
}

export function publishBlocked(payload?: { message?: string; validationIssues?: MicroflowValidationIssue[]; raw?: unknown }): MicroflowApiResponse<never> {
  return fail({
    code: "MICROFLOW_PUBLISH_BLOCKED",
    message: payload?.message ?? "Microflow publish is blocked.",
    validationIssues: payload?.validationIssues,
    raw: payload?.raw,
    httpStatus: 422,
  });
}

export function referenceBlocked(message = "Microflow reference operation is blocked."): MicroflowApiResponse<never> {
  return fail({ code: "MICROFLOW_REFERENCE_BLOCKED", message, httpStatus: 422 });
}

export function serviceUnavailable(message = "Microflow mock service is unavailable."): MicroflowApiResponse<never> {
  return fail({ code: "MICROFLOW_SERVICE_UNAVAILABLE", message, retryable: true, httpStatus: 500 });
}

export function nameDuplicated(message = "Microflow name already exists."): MicroflowApiResponse<never> {
  return fail({ code: "MICROFLOW_NAME_DUPLICATED", message, httpStatus: 409 });
}

export function runFailed(message = "Microflow run failed."): MicroflowApiResponse<never> {
  return fail({ code: "MICROFLOW_RUN_FAILED", message, httpStatus: 500 });
}

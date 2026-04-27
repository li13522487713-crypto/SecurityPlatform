/**
 * Integration/production adapter.
 * Uses backend API contracts and MicroflowApiResponse envelope.
 */
import type { MicroflowApiError, MicroflowApiErrorCode } from "../../contracts/api/api-envelope";

function codeFromStatus(status: number): MicroflowApiErrorCode {
  if (status === 401 || status === 403) {
    return "MICROFLOW_PERMISSION_DENIED";
  }
  if (status === 404) {
    return "MICROFLOW_NOT_FOUND";
  }
  if (status === 409) {
    return "MICROFLOW_VERSION_CONFLICT";
  }
  if (status === 422 || status === 400) {
    return "MICROFLOW_SCHEMA_INVALID";
  }
  return "MICROFLOW_UNKNOWN_ERROR";
}

export class MicroflowApiClientError extends Error {
  readonly status?: number;
  readonly traceId?: string;
  readonly apiError: MicroflowApiError;

  constructor(message: string, options: { status?: number; traceId?: string; apiError?: MicroflowApiError } = {}) {
    super(message);
    this.name = "MicroflowApiClientError";
    this.status = options.status;
    this.traceId = options.traceId;
    this.apiError = options.apiError ?? {
      code: options.status ? codeFromStatus(options.status) : "MICROFLOW_UNKNOWN_ERROR",
      message,
    };
  }
}

export function createMicroflowApiError(message: string, status?: number): MicroflowApiClientError {
  return new MicroflowApiClientError(message, { status });
}

export function isMicroflowApiClientError(error: unknown): error is MicroflowApiClientError {
  return error instanceof MicroflowApiClientError;
}

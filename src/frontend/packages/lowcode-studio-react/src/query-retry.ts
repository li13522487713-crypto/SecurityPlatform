import { LowcodeApiError } from './services/api-core';

export function shouldRetryLowcodeQuery(failureCount: number, error: unknown): boolean {
  if (error instanceof LowcodeApiError && error.status >= 400 && error.status < 500) {
    return false;
  }

  return failureCount < 2;
}

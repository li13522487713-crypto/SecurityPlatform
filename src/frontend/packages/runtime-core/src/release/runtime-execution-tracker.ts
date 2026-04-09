import type { RuntimeExecution, RuntimeExecutionStatus } from "./types";

export interface RuntimeExecutionTrackerItem {
  executionId: string;
  appKey: string;
  pageKey: string;
  releaseId?: string;
  releaseVersion?: number;
  userId?: string;
  tenantId?: string;
  traceId?: string;
  status?: "running" | "success" | "failed";
  errorMessage?: string;
  startedAt?: string;
  finishedAt?: string;
}

const executionMap = new Map<string, RuntimeExecution>();

export function createExecution(item: RuntimeExecutionTrackerItem) {
  const execution: RuntimeExecution = {
    ...item,
    status: "running",
    startedAt: new Date().toISOString()
  };
  executionMap.set(item.executionId, execution);
  return execution;
}

export function completeExecution(
  executionId: string,
  status: RuntimeExecutionStatus,
  error?: { message?: string }
) {
  const current = executionMap.get(executionId);
  if (!current) return;
  executionMap.set(executionId, {
    ...current,
    status,
    errorMessage: error?.message,
    finishedAt: new Date().toISOString()
  });
}

export function removeExecution(executionId: string) {
  executionMap.delete(executionId);
}

export function getExecution(executionId: string) {
  return executionMap.get(executionId);
}

export function getActiveExecution(executionId: string) {
  return getExecution(executionId);
}

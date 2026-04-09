export interface RuntimeExecutionTrackerItem {
  executionId: string;
  appKey: string;
  pageKey: string;
  releaseId?: string;
  releaseVersion?: string;
  userId?: string;
  tenantId?: string;
  traceId?: string;
  status?: "running" | "success" | "failed";
  errorMessage?: string;
  startedAt?: string;
  finishedAt?: string;
}

const executionMap = new Map<string, RuntimeExecutionTrackerItem>();

export function createExecution(item: RuntimeExecutionTrackerItem) {
  executionMap.set(item.executionId, {
    ...item,
    status: "running",
    startedAt: new Date().toISOString()
  });
}

export function completeExecution(
  executionId: string,
  status: "success" | "failed",
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

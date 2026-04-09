export type RuntimeExecutionStatus = "running" | "success" | "failed";

export interface RuntimeExecution {
  executionId: string;
  releaseId?: string;
  releaseVersion?: number;
  appKey: string;
  pageKey: string;
  userId?: string;
  tenantId?: string;
  traceId?: string;
  status: RuntimeExecutionStatus;
  startedAt: string;
  finishedAt?: string;
  errorCode?: string;
  errorMessage?: string;
}

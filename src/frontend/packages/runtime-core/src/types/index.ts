export interface RuntimeManifest {
  appKey: string;
  pageKey: string;
  schema: Record<string, unknown>;
  version?: string;
}

export interface RuntimeExecutionRecord {
  executionId: string;
  status: "running" | "succeeded" | "failed";
  startedAt: string;
  finishedAt?: string;
}

export type AmisSchema = Record<string, unknown>;

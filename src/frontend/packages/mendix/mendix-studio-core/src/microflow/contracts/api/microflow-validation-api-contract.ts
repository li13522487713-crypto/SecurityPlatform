import type { MicroflowAuthoringSchema, MicroflowValidationIssue } from "@atlas/microflow";

import type { MicroflowApiResponse } from "./api-envelope";

/**
 * POST /api/microflows/{id}/validate
 * 与 `ValidateMicroflowRequest`（@atlas/microflow 客户端）不同：此处显式包含 `mode` 以匹配后端规则组。
 */
export interface ValidateMicroflowRequest {
  schema: MicroflowAuthoringSchema;
  mode: "edit" | "save" | "publish" | "testRun";
  includeInfo?: boolean;
  includeWarnings?: boolean;
}

export interface ValidateMicroflowResponse {
  issues: MicroflowValidationIssue[];
  summary: {
    errorCount: number;
    warningCount: number;
    infoCount: number;
  };
  serverValidatedAt: string;
}

export type ValidateMicroflowApiResponse = MicroflowApiResponse<ValidateMicroflowResponse>;

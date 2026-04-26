import type { MicroflowValidationIssue } from "@atlas/microflow";

import type { MicroflowApiResponse } from "../api/api-envelope";
import type { GetMicroflowMetadataResponseBody } from "../api/microflow-metadata-api-contract";
import type { ListMicroflowsResponse } from "../api/microflow-resource-api-contract";

/**
 * 与 `request-response-examples.md` 保持字段一致，用于契约静态示例与单测可引用数据。
 * 不引用真实 `MicroflowAuthoringSchema` 全量，以免样例过胖。
 */
export const exampleListMicroflowsEnvelope: MicroflowApiResponse<ListMicroflowsResponse> = {
  success: true,
  data: {
    items: [],
    total: 0,
    pageIndex: 1,
    pageSize: 20,
    hasMore: false
  },
  traceId: "tr-example-1",
  timestamp: "2026-04-27T00:00:00.000Z"
};

export const exampleMetadataResponseBody: GetMicroflowMetadataResponseBody = {
  entities: [],
  associations: [],
  enumerations: [],
  microflows: [],
  pages: [],
  workflows: [],
  modules: [],
  version: "1.0.0",
  updatedAt: "2026-04-27T00:00:00.000Z"
};

const exampleIssue: MicroflowValidationIssue = {
  id: "val-1",
  severity: "error",
  code: "REQUIRED",
  message: "Parameter missing",
  fieldPath: "parameters[0].name"
};

export const exampleValidationErrorEnvelope: MicroflowApiResponse<null> = {
  success: false,
  error: {
    code: "MICROFLOW_VALIDATION_FAILED",
    message: "Validation failed",
    validationIssues: [exampleIssue]
  },
  timestamp: "2026-04-27T00:00:00.000Z"
};

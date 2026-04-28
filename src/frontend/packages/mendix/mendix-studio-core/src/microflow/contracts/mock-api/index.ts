import type { HttpHandler } from "msw";

import { createMicroflowMockErrorHandlers } from "./mock-error-handlers";
import { createMicroflowMetadataMockHandlers } from "./mock-metadata-handlers";
import { createMicroflowPublishMockHandlers } from "./mock-publish-handlers";
import { createMicroflowReferenceMockHandlers } from "./mock-reference-handlers";
import { createMicroflowResourceMockHandlers } from "./mock-resource-handlers";
import { createMicroflowRuntimeMockHandlers } from "./mock-runtime-handlers";
import { getMicroflowContractMockStore } from "./mock-api-store";
import type { MicroflowContractMockStore } from "./mock-api-types";
import { createMicroflowValidationMockHandlers } from "./mock-validation-handlers";
import { createMicroflowVersionMockHandlers } from "./mock-version-handlers";

export * from "./mock-api-response";
export * from "./mock-api-store";
export * from "./mock-api-types";

export const microflowContractMockOpenApiPaths = [
  "GET /api/microflows",
  "POST /api/microflows",
  "GET /api/microflows/{id}",
  "PATCH /api/microflows/{id}",
  "DELETE /api/microflows/{id}",
  "GET /api/microflows/{id}/schema",
  "PUT /api/microflows/{id}/schema",
  "POST /api/microflows/{id}/schema/migrate",
  "POST /api/microflows/{id}/duplicate",
  "POST /api/microflows/{id}/rename",
  "POST /api/microflows/{id}/favorite",
  "POST /api/microflows/{id}/archive",
  "POST /api/microflows/{id}/restore",
  "POST /api/microflows/{id}/validate",
  "POST /api/microflows/{id}/test-run",
  "POST /api/microflows/runs/{runId}/cancel",
  "GET /api/microflows/runs/{runId}",
  "GET /api/microflows/runs/{runId}/trace",
  "POST /api/microflows/{id}/publish",
  "GET /api/microflows/{id}/versions",
  "GET /api/microflows/{id}/versions/{versionId}",
  "POST /api/microflows/{id}/versions/{versionId}/rollback",
  "POST /api/microflows/{id}/versions/{versionId}/duplicate",
  "GET /api/microflows/{id}/versions/{versionId}/compare-current",
  "GET /api/microflows/{id}/references",
  "GET /api/microflows/{id}/impact",
  "GET /api/microflow-metadata",
  "GET /api/microflow-metadata/entities/{qualifiedName}",
  "GET /api/microflow-metadata/enumerations/{qualifiedName}",
  "GET /api/microflow-metadata/microflows",
] as const;

export function createMicroflowContractMockHandlers(store: MicroflowContractMockStore = getMicroflowContractMockStore()): HttpHandler[] {
  return [
    ...createMicroflowMockErrorHandlers(),
    ...createMicroflowResourceMockHandlers(store),
    ...createMicroflowMetadataMockHandlers(store),
    ...createMicroflowValidationMockHandlers(store),
    ...createMicroflowPublishMockHandlers(store),
    ...createMicroflowVersionMockHandlers(store),
    ...createMicroflowReferenceMockHandlers(store),
    ...createMicroflowRuntimeMockHandlers(store),
  ];
}

export const microflowContractMockHandlers = createMicroflowContractMockHandlers();

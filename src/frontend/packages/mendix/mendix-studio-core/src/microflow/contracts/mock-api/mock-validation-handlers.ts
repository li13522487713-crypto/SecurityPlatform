import { HttpResponse, type HttpHandler } from "msw";

import type { ValidateMicroflowRequest } from "../api/microflow-validation-api-contract";
import type { MicroflowContractMockStore } from "./mock-api-types";
import { ok, schemaInvalid, validationFailed } from "./mock-api-response";
import { mockPost } from "./mock-handler-utils";

export function validateMockMicroflow(store: MicroflowContractMockStore, body: ValidateMicroflowRequest) {
  void store;
  const issues = [];
  const nodeIds = new Set(body.schema.workflow.nodes.map(node => node.id));
  for (const edge of body.schema.workflow.edges) {
    if (!nodeIds.has(edge.sourceNodeID) || !nodeIds.has(edge.targetNodeID)) {
      issues.push({
        code: "MF_EDGE_ENDPOINT_MISSING",
        severity: "error",
        message: "边端点必须引用 workflow.nodes 中存在的节点。",
        source: "workflow.edges",
        fieldPath: `workflow.edges.${edge.id}`
      });
    }
  }
  return {
    issues,
    summary: {
      mode: body.mode,
      errors: issues.filter(issue => issue.severity === "error").length,
      warnings: 0,
      infos: 0,
      total: issues.length
    }
  };
}

export function createMicroflowValidationMockHandlers(store: MicroflowContractMockStore): HttpHandler[] {
  return [
    ...mockPost("/api/microflows/:id/validate", async ({ request }) => {
      const body = await request.json() as ValidateMicroflowRequest;
      if (body.schema?.schemaVersion !== "flowgram.microflow.v1" || !Array.isArray(body.schema.workflow?.nodes) || !Array.isArray(body.schema.workflow?.edges)) {
        return HttpResponse.json(schemaInvalid(), { status: 422 });
      }
      const result = validateMockMicroflow(store, body);
      if (new URL(request.url).searchParams.get("envelopeError") === "validation-failed") {
        return HttpResponse.json(validationFailed(result.issues), { status: 422 });
      }
      return HttpResponse.json(ok({
        issues: result.issues,
        summary: result.summary,
        serverValidatedAt: new Date().toISOString(),
      }));
    }),
  ];
}

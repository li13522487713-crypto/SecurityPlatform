import { validateMicroflowSchema } from "@atlas/microflow";
import { HttpResponse, type HttpHandler } from "msw";

import type { ValidateMicroflowRequest } from "../api/microflow-validation-api-contract";
import type { MicroflowContractMockStore } from "./mock-api-types";
import { ok, schemaInvalid, validationFailed } from "./mock-api-response";
import { mockPost } from "./mock-handler-utils";

export function validateMockMicroflow(store: MicroflowContractMockStore, body: ValidateMicroflowRequest) {
  return validateMicroflowSchema({
    schema: body.schema,
    metadata: store.metadataCatalog,
    options: {
      mode: body.mode,
      includeInfo: body.includeInfo ?? true,
      includeWarnings: body.includeWarnings ?? true,
    },
  });
}

export function createMicroflowValidationMockHandlers(store: MicroflowContractMockStore): HttpHandler[] {
  return [
    ...mockPost("/api/microflows/:id/validate", async ({ request }) => {
      const body = await request.json() as ValidateMicroflowRequest;
      if (!body.schema?.objectCollection) {
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

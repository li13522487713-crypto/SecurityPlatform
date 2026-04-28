import { HttpResponse, type HttpHandler } from "msw";

import type { MicroflowImpactLevel, MicroflowReferenceSourceType } from "../../references/microflow-reference-types";
import { analyzeMockImpact, getMockReferences, getMockResource } from "./mock-api-store";
import type { MicroflowContractMockStore } from "./mock-api-types";
import { notFound, ok } from "./mock-api-response";
import { mockGet } from "./mock-handler-utils";

function getArray(searchParams: URLSearchParams, key: string): string[] {
  return searchParams.getAll(key).flatMap(value => value.split(",")).map(value => value.trim()).filter(Boolean);
}

export function createMicroflowReferenceMockHandlers(store: MicroflowContractMockStore): HttpHandler[] {
  return [
    ...mockGet("/api/microflows/:id/references", ({ request, params }) => {
      if (!store.resources.has(params.id)) {
        return HttpResponse.json(notFound(), { status: 404 });
      }
      const searchParams = new URL(request.url).searchParams;
      const includeInactive = searchParams.get("includeInactive") === "true";
      const sourceType = getArray(searchParams, "sourceType") as MicroflowReferenceSourceType[];
      const impactLevel = getArray(searchParams, "impactLevel") as MicroflowImpactLevel[];
      const references = getMockReferences(store, params.id)
        .filter(ref => includeInactive || ref.active !== false)
        .filter(ref => !sourceType.length || sourceType.includes(ref.sourceType))
        .filter(ref => !impactLevel.length || impactLevel.includes(ref.impactLevel));
      return HttpResponse.json(ok(references));
    }),
    ...mockGet("/api/microflows/:id/impact", ({ request, params }) => {
      const resource = getMockResource(store, params.id);
      if (!resource) {
        return HttpResponse.json(notFound(), { status: 404 });
      }
      const searchParams = new URL(request.url).searchParams;
      return HttpResponse.json(ok(analyzeMockImpact(store, resource, searchParams.get("version") ?? resource.version)));
    }),
  ];
}

import { HttpResponse, type HttpHandler } from "msw";

import type { MicroflowContractMockStore } from "./mock-api-types";
import { getMockMetadataCatalog } from "./mock-api-store";
import { metadataNotFound, ok } from "./mock-api-response";
import { mockGet } from "./mock-handler-utils";

function decode(value: string): string {
  return decodeURIComponent(value);
}

export function createMicroflowMetadataMockHandlers(store: MicroflowContractMockStore): HttpHandler[] {
  return [
    ...mockGet("/api/microflow-metadata", () => {
      const catalog = getMockMetadataCatalog(store);
      return HttpResponse.json(ok({
        ...catalog,
        updatedAt: new Date().toISOString(),
        catalogVersion: catalog.version ?? "mock-1",
      }));
    }),
    ...mockGet("/api/microflow-metadata/entities/:qualifiedName", ({ params }) => {
      const qualifiedName = decode(params.qualifiedName);
      const entity = store.metadataCatalog.entities.find(item => item.qualifiedName === qualifiedName);
      return entity ? HttpResponse.json(ok(entity)) : HttpResponse.json(metadataNotFound(`Entity ${qualifiedName} was not found.`), { status: 404 });
    }),
    ...mockGet("/api/microflow-metadata/enumerations/:qualifiedName", ({ params }) => {
      const qualifiedName = decode(params.qualifiedName);
      const enumeration = store.metadataCatalog.enumerations.find(item => item.qualifiedName === qualifiedName);
      return enumeration ? HttpResponse.json(ok(enumeration)) : HttpResponse.json(metadataNotFound(`Enumeration ${qualifiedName} was not found.`), { status: 404 });
    }),
    ...mockGet("/api/microflow-metadata/microflows", ({ request }) => {
      const searchParams = new URL(request.url).searchParams;
      const keyword = searchParams.get("keyword")?.trim().toLowerCase();
      const moduleId = searchParams.get("moduleId");
      const refs = store.metadataCatalog.microflows
        .filter(item => !moduleId || item.moduleName === moduleId)
        .filter(item => !keyword || [item.name, item.qualifiedName, item.moduleName].filter(Boolean).join(" ").toLowerCase().includes(keyword));
      return HttpResponse.json(ok(refs));
    }),
    ...mockGet("/api/microflow-metadata/pages", () => HttpResponse.json(ok(store.metadataCatalog.pages))),
    ...mockGet("/api/microflow-metadata/workflows", () => HttpResponse.json(ok(store.metadataCatalog.workflows))),
  ];
}

import { normalizeMicroflowSchema } from "@atlas/microflow";
import { HttpResponse, type HttpHandler } from "msw";

import type { DuplicateMicroflowVersionRequest } from "../api/microflow-version-api-contract";
import { diffMicroflowSchemas } from "../../versions/microflow-version-diff";
import { clone, defaultMockPermissions, getMockResource, makeMockId, saveMockResource } from "./mock-api-store";
import type { MicroflowContractMockStore } from "./mock-api-types";
import { notFound, ok } from "./mock-api-response";
import { mockGet, mockPost } from "./mock-handler-utils";

function findVersion(store: MicroflowContractMockStore, id: string, versionId: string) {
  return (store.versions.get(id) ?? []).find(item => item.id === versionId || item.version === versionId);
}

export function createMicroflowVersionMockHandlers(store: MicroflowContractMockStore): HttpHandler[] {
  return [
    ...mockGet("/api/microflows/:id/versions", ({ params }) => {
      if (!store.resources.has(params.id)) {
        return HttpResponse.json(notFound(), { status: 404 });
      }
      return HttpResponse.json(ok(store.versions.get(params.id) ?? []));
    }),
    ...mockGet("/api/microflows/:id/versions/:versionId", ({ params }) => {
      const current = getMockResource(store, params.id);
      const version = findVersion(store, params.id, params.versionId);
      const snapshot = version ? store.publishSnapshots.get(version.schemaSnapshotId) : undefined;
      if (!current || !version || !snapshot) {
        return HttpResponse.json(notFound("Microflow version was not found."), { status: 404 });
      }
      return HttpResponse.json(ok({ ...version, snapshot, diffFromCurrent: diffMicroflowSchemas(snapshot.schema, current.schema) }));
    }),
    ...mockPost("/api/microflows/:id/versions/:versionId/rollback", ({ params }) => {
      const current = getMockResource(store, params.id);
      const version = findVersion(store, params.id, params.versionId);
      const snapshot = version ? store.publishSnapshots.get(version.schemaSnapshotId) : undefined;
      if (!current || !version || !snapshot) {
        return HttpResponse.json(notFound("Microflow version was not found."), { status: 404 });
      }
      const timestamp = new Date().toISOString();
      const schema = normalizeMicroflowSchema(clone(snapshot.schema) as unknown);
      schema.audit = { ...schema.audit, status: "draft", updatedAt: timestamp };
      const resource = saveMockResource(store, {
        ...current,
        name: schema.name,
        displayName: schema.displayName,
        description: schema.description,
        schema,
        status: "draft",
        publishStatus: current.latestPublishedVersion ? "changedAfterPublish" : "neverPublished",
        updatedAt: timestamp,
      });
      store.versions.set(params.id, [{
        ...version,
        id: makeMockId("version"),
        status: "rolledBack",
        createdAt: timestamp,
        description: `Rolled back from ${version.version}`,
        isLatestPublished: false,
      }, ...(store.versions.get(params.id) ?? [])]);
      return HttpResponse.json(ok(resource));
    }),
    ...mockPost("/api/microflows/:id/versions/:versionId/duplicate", async ({ request, params }) => {
      const current = getMockResource(store, params.id);
      const version = findVersion(store, params.id, params.versionId);
      const snapshot = version ? store.publishSnapshots.get(version.schemaSnapshotId) : undefined;
      if (!current || !version || !snapshot) {
        return HttpResponse.json(notFound("Microflow version was not found."), { status: 404 });
      }
      const input = await request.json() as DuplicateMicroflowVersionRequest;
      const timestamp = new Date().toISOString();
      const id = makeMockId("mf");
      const schema = normalizeMicroflowSchema(clone(snapshot.schema) as unknown);
      schema.id = id;
      schema.stableId = id;
      schema.name = input.name || `${current.name}VersionCopy`;
      schema.displayName = input.displayName || `${current.displayName || current.name} ${version.version} Copy`;
      schema.audit = { ...schema.audit, version: "0.1.0", status: "draft", createdAt: timestamp, updatedAt: timestamp };
      const duplicate = saveMockResource(store, {
        ...current,
        id,
        schemaId: id,
        name: schema.name,
        displayName: schema.displayName,
        moduleId: input.moduleId || current.moduleId,
        moduleName: input.moduleName || current.moduleName,
        tags: input.tags ?? [...current.tags],
        createdAt: timestamp,
        updatedAt: timestamp,
        version: "0.1.0",
        latestPublishedVersion: undefined,
        status: "draft",
        publishStatus: "neverPublished",
        favorite: false,
        archived: false,
        referenceCount: 0,
        schema,
        permissions: defaultMockPermissions(),
      });
      store.versions.set(id, []);
      store.references.set(id, []);
      return HttpResponse.json(ok(duplicate));
    }),
    ...mockGet("/api/microflows/:id/versions/:versionId/compare-current", ({ params }) => {
      const current = getMockResource(store, params.id);
      const version = findVersion(store, params.id, params.versionId);
      const snapshot = version ? store.publishSnapshots.get(version.schemaSnapshotId) : undefined;
      if (!current || !version || !snapshot) {
        return HttpResponse.json(notFound("Microflow version was not found."), { status: 404 });
      }
      return HttpResponse.json(ok(diffMicroflowSchemas(snapshot.schema, current.schema)));
    }),
  ];
}

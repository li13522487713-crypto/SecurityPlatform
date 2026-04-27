import { normalizeMicroflowSchema, type MicroflowAuthoringSchema } from "@atlas/microflow";
import { HttpResponse, type HttpHandler } from "msw";

import type { CreateMicroflowRequest, DuplicateMicroflowRequest, RenameMicroflowRequest, ToggleFavoriteMicroflowRequest, UpdateMicroflowResourceRequest } from "../api/microflow-resource-api-contract";
import type { MigrateMicroflowSchemaRequest, SaveMicroflowSchemaRequest } from "../api/microflow-schema-api-contract";
import type { MicroflowApiPageResult } from "../api/api-envelope";
import type { MicroflowCreateInput, MicroflowPublishStatus, MicroflowResource, MicroflowResourceQuery, MicroflowResourceStatus } from "../../resource/resource-types";
import { createDefaultMicroflowSchema } from "../../schema/create-default-microflow-schema";
import { clone, createMockSchemaSnapshot, defaultMockPermissions, getMockResource, makeMockId, saveMockResource } from "./mock-api-store";
import type { MicroflowContractMockStore } from "./mock-api-types";
import { nameDuplicated, notFound, ok, schemaInvalid, versionConflict } from "./mock-api-response";
import { mockDelete, mockGet, mockPatch, mockPost, mockPut } from "./mock-handler-utils";

function getArray(searchParams: URLSearchParams, key: string): string[] {
  return searchParams.getAll(key).flatMap(value => value.split(",")).map(value => value.trim()).filter(Boolean);
}

function readBoolean(value: string | null): boolean | undefined {
  if (value === "true") {
    return true;
  }
  if (value === "false") {
    return false;
  }
  return undefined;
}

function parseResourceQuery(request: Request): MicroflowResourceQuery {
  const searchParams = new URL(request.url).searchParams;
  return {
    pageIndex: Number(searchParams.get("pageIndex") || "1"),
    pageSize: Number(searchParams.get("pageSize") || "20"),
    keyword: searchParams.get("keyword") ?? undefined,
    workspaceId: searchParams.get("workspaceId") ?? undefined,
    status: getArray(searchParams, "status") as MicroflowResourceStatus[],
    publishStatus: getArray(searchParams, "publishStatus") as MicroflowPublishStatus[],
    favoriteOnly: readBoolean(searchParams.get("favoriteOnly")),
    moduleId: searchParams.get("moduleId") ?? undefined,
    tags: getArray(searchParams, "tags"),
    sortBy: (searchParams.get("sortBy") as MicroflowResourceQuery["sortBy"]) || "updatedAt",
    sortOrder: (searchParams.get("sortOrder") as MicroflowResourceQuery["sortOrder"]) || "desc",
  };
}

function isInRange(value: string, from?: string, to?: string): boolean {
  const time = new Date(value).getTime();
  if (Number.isNaN(time)) {
    return false;
  }
  if (from && time < new Date(from).getTime()) {
    return false;
  }
  if (to && time > new Date(to).getTime()) {
    return false;
  }
  return true;
}

function listResources(store: MicroflowContractMockStore, query: MicroflowResourceQuery): MicroflowApiPageResult<MicroflowResource> {
  const keyword = query.keyword?.trim().toLowerCase();
  const tags = query.tags ?? [];
  const sortBy = query.sortBy ?? "updatedAt";
  const sortOrder = query.sortOrder ?? "desc";
  const pageIndex = query.pageIndex && query.pageIndex > 0 ? query.pageIndex : 1;
  const pageSize = query.pageSize && query.pageSize > 0 ? query.pageSize : 20;
  const items = [...store.resources.values()]
    .filter(resource => (query.workspaceId ? resource.workspaceId === query.workspaceId : true))
    .filter(resource => !query.status?.length || query.status.includes(resource.status))
    .filter(resource => !query.publishStatus?.length || query.publishStatus.includes(resource.publishStatus ?? "neverPublished"))
    .filter(resource => !query.favoriteOnly || resource.favorite)
    .filter(resource => !query.moduleId || resource.moduleId === query.moduleId)
    .filter(resource => !tags.length || tags.some(tag => resource.tags.includes(tag)))
    .filter(resource => isInRange(resource.updatedAt, query.updatedFrom, query.updatedTo))
    .filter(resource => !keyword || [resource.name, resource.displayName, resource.description, ...resource.tags].filter(Boolean).join(" ").toLowerCase().includes(keyword))
    .sort((left, right) => {
      const direction = sortOrder === "asc" ? 1 : -1;
      if (sortBy === "name" || sortBy === "version") {
        return direction * left[sortBy].localeCompare(right[sortBy], undefined, { numeric: true, sensitivity: "base" });
      }
      if (sortBy === "referenceCount") {
        return direction * (left.referenceCount - right.referenceCount);
      }
      return direction * (new Date(left[sortBy as "updatedAt" | "createdAt"]).getTime() - new Date(right[sortBy as "updatedAt" | "createdAt"]).getTime());
    });
  const start = (pageIndex - 1) * pageSize;
  const page = items.slice(start, start + pageSize);
  return { items: clone(page), total: items.length, pageIndex, pageSize, hasMore: start + page.length < items.length };
}

function createResource(input: MicroflowCreateInput, request: Request, workspaceId?: string): MicroflowResource {
  const timestamp = new Date().toISOString();
  const currentUser = {
    id: request.headers.get("X-User-Id") || "current-user",
    name: request.headers.get("X-User-Id") || "Current User",
  };
  const id = makeMockId("mf");
  const schema = createDefaultMicroflowSchema({ ...input, id, ownerName: currentUser.name });
  return {
    id,
    schemaId: id,
    workspaceId,
    moduleId: input.moduleId,
    moduleName: input.moduleName,
    name: input.name,
    displayName: input.displayName || input.name,
    description: input.description,
    tags: [...input.tags],
    ownerId: currentUser.id,
    ownerName: currentUser.name,
    createdBy: currentUser.name,
    createdAt: timestamp,
    updatedBy: currentUser.name,
    updatedAt: timestamp,
    version: schema.audit.version,
    status: "draft",
    publishStatus: "neverPublished",
    favorite: false,
    archived: false,
    referenceCount: 0,
    lastRunStatus: "neverRun",
    schema,
    permissions: defaultMockPermissions(),
  };
}

function duplicateResource(store: MicroflowContractMockStore, source: MicroflowResource, input: DuplicateMicroflowRequest = {}): MicroflowResource {
  const timestamp = new Date().toISOString();
  const id = makeMockId("mf");
  const schema = normalizeMicroflowSchema(clone(source.schema) as unknown);
  schema.id = id;
  schema.stableId = id;
  schema.name = input.name || `${source.name}Copy`;
  schema.displayName = input.displayName || `${source.displayName || source.name} Copy`;
  schema.moduleId = input.moduleId || source.moduleId;
  schema.moduleName = input.moduleName || source.moduleName;
  schema.audit = { ...schema.audit, version: "0.1.0", status: "draft", createdAt: timestamp, updatedAt: timestamp };
  const resource: MicroflowResource = {
    ...source,
    id,
    schemaId: id,
    name: schema.name,
    displayName: schema.displayName,
    moduleId: schema.moduleId,
    moduleName: schema.moduleName,
    tags: input.tags ?? [...source.tags],
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
  };
  saveMockResource(store, resource);
  store.versions.set(id, []);
  store.references.set(id, []);
  return resource;
}

export function createMicroflowResourceMockHandlers(store: MicroflowContractMockStore): HttpHandler[] {
  return [
    ...mockGet("/api/microflows", ({ request }) => HttpResponse.json(ok(listResources(store, parseResourceQuery(request))))),
    ...mockPost("/api/microflows", async ({ request }) => {
      const body = await request.json() as CreateMicroflowRequest;
      const input = body.input;
      if (!input?.name?.trim()) {
        return HttpResponse.json(schemaInvalid("Microflow name is required."), { status: 422 });
      }
      if ([...store.resources.values()].some(item => item.name.toLowerCase() === input.name.trim().toLowerCase())) {
        return HttpResponse.json(nameDuplicated(), { status: 409 });
      }
      const resource = saveMockResource(store, createResource(input, request, body.workspaceId ?? request.headers.get("X-Workspace-Id") ?? undefined));
      store.versions.set(resource.id, []);
      store.references.set(resource.id, []);
      return HttpResponse.json(ok(resource));
    }),
    ...mockGet("/api/microflows/:id", ({ params }) => {
      const resource = getMockResource(store, params.id);
      return resource ? HttpResponse.json(ok(resource)) : HttpResponse.json(notFound(), { status: 404 });
    }),
    ...mockPatch("/api/microflows/:id", async ({ request, params }) => {
      const current = getMockResource(store, params.id);
      if (!current) {
        return HttpResponse.json(notFound(), { status: 404 });
      }
      const body = await request.json() as UpdateMicroflowResourceRequest;
      const next = saveMockResource(store, { ...current, ...body.patch, updatedAt: new Date().toISOString() });
      return HttpResponse.json(ok(next));
    }),
    ...mockGet("/api/microflows/:id/schema", ({ params }) => {
      const resource = getMockResource(store, params.id);
      if (!resource) {
        return HttpResponse.json(notFound(), { status: 404 });
      }
      return HttpResponse.json(ok({
        resourceId: resource.id,
        schema: resource.schema,
        schemaVersion: resource.schema.schemaVersion,
        migrationVersion: "mock-migration-1",
        updatedAt: resource.updatedAt,
        updatedBy: resource.updatedBy,
      }));
    }),
    ...mockPut("/api/microflows/:id/schema", async ({ request, params }) => {
      const current = getMockResource(store, params.id);
      if (!current) {
        return HttpResponse.json(notFound(), { status: 404 });
      }
      const body = await request.json() as SaveMicroflowSchemaRequest;
      if (body.baseVersion === "conflict") {
        return HttpResponse.json(versionConflict(), { status: 409 });
      }
      if (!body.schema?.objectCollection) {
        return HttpResponse.json(schemaInvalid(), { status: 422 });
      }
      const timestamp = new Date().toISOString();
      const schema = normalizeMicroflowSchema(clone(body.schema) as unknown);
      schema.id = current.schemaId;
      schema.audit = { ...schema.audit, version: current.version, status: current.status === "published" ? "draft" : schema.audit.status, updatedAt: timestamp };
      const resource = saveMockResource(store, {
        ...current,
        name: schema.name,
        displayName: schema.displayName || current.displayName,
        description: schema.description,
        schema,
        publishStatus: current.latestPublishedVersion ? "changedAfterPublish" : "neverPublished",
        updatedAt: timestamp,
      });
      createMockSchemaSnapshot(store, resource.id, schema);
      return HttpResponse.json(ok({
        resource,
        schemaVersion: schema.schemaVersion,
        updatedAt: timestamp,
        changedAfterPublish: resource.publishStatus === "changedAfterPublish",
      }));
    }),
    ...mockPost("/api/microflows/:id/schema/migrate", async ({ request, params }) => {
      const current = getMockResource(store, params.id);
      if (!current) {
        return HttpResponse.json(notFound(), { status: 404 });
      }
      const body = await request.json() as MigrateMicroflowSchemaRequest;
      const schema = normalizeMicroflowSchema(body.schema as unknown);
      return HttpResponse.json(ok({ schema, warnings: body.fromVersion === body.toVersion ? [] : [`Mock migrated from ${body.fromVersion} to ${body.toVersion}.`] }));
    }),
    ...mockPost("/api/microflows/:id/duplicate", async ({ request, params }) => {
      const current = getMockResource(store, params.id);
      if (!current) {
        return HttpResponse.json(notFound(), { status: 404 });
      }
      return HttpResponse.json(ok(duplicateResource(store, current, await request.json() as DuplicateMicroflowRequest)));
    }),
    ...mockPost("/api/microflows/:id/rename", async ({ request, params }) => {
      const current = getMockResource(store, params.id);
      if (!current) {
        return HttpResponse.json(notFound(), { status: 404 });
      }
      const body = await request.json() as RenameMicroflowRequest;
      const schema = { ...current.schema, name: body.name, displayName: body.displayName || body.name };
      return HttpResponse.json(ok(saveMockResource(store, { ...current, name: body.name, displayName: body.displayName || body.name, schema, updatedAt: new Date().toISOString() })));
    }),
    ...mockPost("/api/microflows/:id/favorite", async ({ request, params }) => {
      const current = getMockResource(store, params.id);
      if (!current) {
        return HttpResponse.json(notFound(), { status: 404 });
      }
      const body = await request.json() as ToggleFavoriteMicroflowRequest;
      return HttpResponse.json(ok(saveMockResource(store, { ...current, favorite: body.favorite, updatedAt: new Date().toISOString() })));
    }),
    ...mockPost("/api/microflows/:id/archive", ({ params }) => {
      const current = getMockResource(store, params.id);
      if (!current) {
        return HttpResponse.json(notFound(), { status: 404 });
      }
      return HttpResponse.json(ok(saveMockResource(store, { ...current, status: "archived", archived: true, permissions: { ...(current.permissions ?? defaultMockPermissions()), canPublish: false }, updatedAt: new Date().toISOString() })));
    }),
    ...mockPost("/api/microflows/:id/restore", ({ params }) => {
      const current = getMockResource(store, params.id);
      if (!current) {
        return HttpResponse.json(notFound(), { status: 404 });
      }
      return HttpResponse.json(ok(saveMockResource(store, { ...current, status: "draft", archived: false, permissions: defaultMockPermissions(), updatedAt: new Date().toISOString() })));
    }),
    ...mockDelete("/api/microflows/:id", ({ params }) => {
      if (!store.resources.has(params.id)) {
        return HttpResponse.json(notFound(), { status: 404 });
      }
      store.resources.delete(params.id);
      store.schemaSnapshots.delete(params.id);
      store.versions.delete(params.id);
      store.references.delete(params.id);
      return HttpResponse.json(ok({ id: params.id }));
    }),
  ];
}

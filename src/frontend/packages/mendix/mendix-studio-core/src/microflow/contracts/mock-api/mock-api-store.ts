import type { MicroflowDesignSchema, MicroflowMetadataCatalog, MicroflowRunSession } from "@atlas/microflow";
import { getDefaultMockMetadataCatalog } from "@atlas/microflow/metadata";

import { analyzeMicroflowPublishImpact, hashSchemaSnapshot, summarizeValidation } from "../../publish/microflow-publish-utils";
import type { MicroflowReference } from "../../references/microflow-reference-types";
import type { MicroflowResource } from "../../resource/resource-types";
import type { MicroflowPublishedSnapshot, MicroflowVersionSummary } from "../../versions/microflow-version-types";
import type { MicroflowContractMockSeedOptions, MicroflowContractMockStore, MicroflowSchemaSnapshotContract } from "./mock-api-types";

function nowIso(): string {
  return new Date().toISOString();
}

export function makeMockId(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
}

export function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

export function defaultMockPermissions() {
  return {
    canEdit: true,
    canDelete: true,
    canPublish: true,
    canArchive: true,
    canDuplicate: true,
  };
}

function createSchemaSnapshot(resource: MicroflowResource): MicroflowSchemaSnapshotContract {
  return {
    resourceId: resource.id,
    schema: clone(resource.schema),
    schemaVersion: resource.schema.schemaVersion,
    updatedAt: resource.updatedAt,
    updatedBy: resource.updatedBy,
  };
}

export function createMockReferences(resourceId: string, referencedVersion?: string): MicroflowReference[] {
  return [
    {
      id: `${resourceId}-mf-ref`,
      targetMicroflowId: resourceId,
      sourceType: "microflow",
      active: true,
      sourceName: "OrderFulfillment 调用微流",
      sourceId: "mf-parent-flow",
      sourceVersion: "1.3.0",
      referencedVersion,
      referenceKind: "callMicroflow",
      impactLevel: "medium",
      description: "另一个微流将该资源作为可复用动作调用。",
      canNavigate: true,
    },
    {
      id: `${resourceId}-page-ref`,
      targetMicroflowId: resourceId,
      sourceType: "button",
      active: true,
      sourceName: "订单详情页提交按钮",
      sourcePath: "/pages/order/detail",
      sourceVersion: "draft",
      referencedVersion,
      referenceKind: "pageAction",
      impactLevel: "low",
      description: "页面按钮点击后触发该微流。",
      canNavigate: true,
    },
    {
      id: `${resourceId}-workflow-ref`,
      targetMicroflowId: resourceId,
      sourceType: "workflow",
      active: true,
      sourceName: "订单审批工作流活动",
      sourceId: "wf-order-approval",
      sourceVersion: "2.1.0",
      referencedVersion,
      referenceKind: "workflowActivity",
      impactLevel: "high",
      description: "工作流活动依赖当前发布契约。",
      canNavigate: true,
    },
  ];
}

export function createMockPublishSnapshot(
  resourceId: string,
  version: string,
  schema: MicroflowDesignSchema,
  input: { publishedAt?: string; publishedBy?: string; description?: string } = {},
): MicroflowPublishedSnapshot {
  const normalized = clone(schema);
  return {
    id: `${resourceId}@${version}`,
    resourceId,
    version,
    schema: normalized,
    publishedAt: input.publishedAt ?? nowIso(),
    publishedBy: input.publishedBy,
    description: input.description,
    validationSummary: summarizeValidation(normalized, getDefaultMockMetadataCatalog()),
    schemaHash: hashSchemaSnapshot(normalized),
  };
}

export function createMockVersionSummary(snapshot: MicroflowPublishedSnapshot, input: { createdBy?: string; referenceCount?: number; isLatestPublished?: boolean } = {}): MicroflowVersionSummary {
  return {
    id: `version-${snapshot.resourceId}-${snapshot.version}`,
    resourceId: snapshot.resourceId,
    version: snapshot.version,
    status: "published",
    createdAt: snapshot.publishedAt,
    createdBy: input.createdBy ?? snapshot.publishedBy,
    description: snapshot.description,
    schemaSnapshotId: snapshot.id,
    validationSummary: snapshot.validationSummary,
    referenceCount: input.referenceCount,
    isLatestPublished: input.isLatestPublished ?? true,
  };
}

export function createMicroflowContractMockStore(options: MicroflowContractMockSeedOptions = {}): MicroflowContractMockStore {
  const store: MicroflowContractMockStore = {
    resources: new Map(),
    schemaSnapshots: new Map(),
    versions: new Map(),
    publishSnapshots: new Map(),
    references: new Map(),
    runSessions: new Map(),
    metadataCatalog: clone(getDefaultMockMetadataCatalog()) as MicroflowMetadataCatalog,
  };
  seedMicroflowContractMockStore(store, options);
  return store;
}

export function seedMicroflowContractMockStore(store: MicroflowContractMockStore, options: MicroflowContractMockSeedOptions = {}): MicroflowContractMockStore {
  store.resources.clear();
  store.schemaSnapshots.clear();
  store.versions.clear();
  store.publishSnapshots.clear();
  store.references.clear();
  store.runSessions.clear();
  store.metadataCatalog = clone(getDefaultMockMetadataCatalog()) as MicroflowMetadataCatalog;

  void options;
  return store;
}

const singletonStore = createMicroflowContractMockStore();

export function getMicroflowContractMockStore(): MicroflowContractMockStore {
  return singletonStore;
}

export function resetMicroflowContractMockStore(options?: MicroflowContractMockSeedOptions): MicroflowContractMockStore {
  return seedMicroflowContractMockStore(singletonStore, options);
}

export function getMockResource(store: MicroflowContractMockStore, id: string): MicroflowResource | undefined {
  const resource = store.resources.get(id);
  return resource ? clone(resource) : undefined;
}

export function saveMockResource(store: MicroflowContractMockStore, resource: MicroflowResource): MicroflowResource {
  const normalized = { ...resource, schema: clone(resource.schema) };
  store.resources.set(resource.id, clone(normalized));
  store.schemaSnapshots.set(resource.id, createSchemaSnapshot(normalized));
  return clone(normalized);
}

export function createMockSchemaSnapshot(store: MicroflowContractMockStore, resourceId: string, schema: MicroflowDesignSchema): MicroflowSchemaSnapshotContract {
  const timestamp = nowIso();
  const snapshot: MicroflowSchemaSnapshotContract = {
    resourceId,
    schema: clone(schema),
    schemaVersion: schema.schemaVersion,
    updatedAt: timestamp,
  };
  store.schemaSnapshots.set(resourceId, snapshot);
  return clone(snapshot);
}

export function createMockRunSession(store: MicroflowContractMockStore, session: MicroflowRunSession): MicroflowRunSession {
  store.runSessions.set(session.id, clone(session));
  return clone(session);
}

export function getMockReferences(store: MicroflowContractMockStore, resourceId: string): MicroflowReference[] {
  return clone(store.references.get(resourceId) ?? []);
}

export function getMockMetadataCatalog(store: MicroflowContractMockStore): MicroflowMetadataCatalog {
  return clone(store.metadataCatalog);
}

export function analyzeMockImpact(store: MicroflowContractMockStore, resource: MicroflowResource, nextVersion: string) {
  const latestSnapshot = [...store.publishSnapshots.values()]
    .filter(snapshot => snapshot.resourceId === resource.id && snapshot.version === resource.latestPublishedVersion)
    .sort((left, right) => right.publishedAt.localeCompare(left.publishedAt))[0];
  return analyzeMicroflowPublishImpact({
    resourceId: resource.id,
    currentVersion: resource.latestPublishedVersion,
    nextVersion,
    currentSchema: resource.schema,
    latestSnapshot,
    references: getMockReferences(store, resource.id),
  });
}

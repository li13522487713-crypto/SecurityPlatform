import { normalizeMicroflowSchema, type MicroflowAuthoringSchema, type MicroflowMetadataCatalog, type MicroflowRunSession } from "@atlas/microflow";
import { getDefaultMockMetadataCatalog } from "@atlas/microflow/metadata";

import { analyzeMicroflowPublishImpact, hashSchemaSnapshot, summarizeValidation } from "../../publish/microflow-publish-utils";
import type { MicroflowReference } from "../../references/microflow-reference-types";
import type { MicroflowCreateInput, MicroflowResource } from "../../resource/resource-types";
import { createDefaultMicroflowSchema } from "../../schema/create-default-microflow-schema";
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

function createResourceFromInput(
  input: MicroflowCreateInput,
  options: Required<MicroflowContractMockSeedOptions>,
  overrides: Partial<MicroflowResource> = {},
): MicroflowResource {
  const id = overrides.id ?? makeMockId("mf");
  const timestamp = overrides.createdAt ?? nowIso();
  const schema = normalizeMicroflowSchema(createDefaultMicroflowSchema({ ...input, id, ownerName: options.currentUser.name }) as unknown);
  const nextSchema: MicroflowAuthoringSchema = {
    ...schema,
    audit: {
      ...schema.audit,
      version: overrides.version ?? schema.audit.version,
      status: overrides.status === "published" ? "published" : schema.audit.status,
      createdAt: timestamp,
      createdBy: options.currentUser.name,
      updatedAt: overrides.updatedAt ?? timestamp,
      updatedBy: options.currentUser.name,
    },
  };
  return {
    id,
    schemaId: id,
    workspaceId: options.workspaceId,
    moduleId: input.moduleId,
    moduleName: input.moduleName,
    name: input.name,
    displayName: input.displayName || input.name,
    description: input.description,
    tags: [...input.tags],
    ownerId: options.currentUser.id,
    ownerName: options.currentUser.name,
    createdBy: options.currentUser.name,
    createdAt: timestamp,
    updatedBy: options.currentUser.name,
    updatedAt: overrides.updatedAt ?? timestamp,
    version: overrides.version ?? nextSchema.audit.version,
    latestPublishedVersion: overrides.latestPublishedVersion,
    status: overrides.status ?? "draft",
    publishStatus: overrides.publishStatus ?? "neverPublished",
    favorite: overrides.favorite ?? false,
    archived: overrides.archived ?? false,
    referenceCount: overrides.referenceCount ?? 0,
    lastRunStatus: overrides.lastRunStatus ?? "neverRun",
    lastRunAt: overrides.lastRunAt,
    schema: nextSchema,
    permissions: overrides.permissions ?? defaultMockPermissions(),
    ...overrides,
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
  schema: MicroflowAuthoringSchema,
  input: { publishedAt?: string; publishedBy?: string; description?: string } = {},
): MicroflowPublishedSnapshot {
  const normalized = normalizeMicroflowSchema(clone(schema) as unknown);
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

function seedResourceSet(options: Required<MicroflowContractMockSeedOptions>): MicroflowResource[] {
  const publishedAt = nowIso();
  const published = createResourceFromInput({
    name: "OrderProcessing",
    displayName: "订单处理微流",
    description: "处理订单校验、库存检查与提交的示例微流。",
    moduleId: "sales",
    moduleName: "Sales",
    tags: ["order", "inventory", "published"],
    parameters: [{ id: "param-order-id", stableId: "param-order-id", name: "orderId", dataType: { kind: "string" }, required: true }],
    returnType: { kind: "boolean" },
    returnVariableName: "isProcessed",
    template: "orderProcessing",
  }, options, {
    id: "mf-order-process",
    status: "published",
    publishStatus: "published",
    latestPublishedVersion: "1.0.0",
    version: "1.0.0",
    favorite: true,
    referenceCount: 3,
    lastRunStatus: "success",
    lastRunAt: publishedAt,
  });

  const changedSchema = clone(published.schema);
  changedSchema.id = "mf-order-breaking";
  changedSchema.stableId = "mf-order-breaking";
  changedSchema.name = "OrderProcessingV2Draft";
  changedSchema.displayName = "订单处理微流 V2 草稿";
  changedSchema.parameters = [];
  changedSchema.returnType = { kind: "primitive", name: "String" };
  changedSchema.audit = { ...changedSchema.audit, status: "draft", updatedAt: publishedAt, updatedBy: options.currentUser.name };
  const changedAfterPublish: MicroflowResource = {
    ...published,
    id: "mf-order-breaking",
    schemaId: "mf-order-breaking",
    name: changedSchema.name,
    displayName: changedSchema.displayName,
    description: "基于已发布版本修改了参数和返回类型，用于演示发布影响分析。",
    publishStatus: "changedAfterPublish",
    latestPublishedVersion: "1.0.0",
    referenceCount: 2,
    favorite: false,
    schema: changedSchema,
  };

  const draft = createResourceFromInput({
    name: "CustomerOnboarding",
    displayName: "用户注册微流",
    description: "新用户注册、资料初始化和欢迎消息编排。",
    moduleId: "customer",
    moduleName: "Customer",
    tags: ["customer", "draft"],
    parameters: [],
    returnType: { kind: "void" },
    template: "blank",
  }, options, { id: "mf-customer-onboarding" });

  const archived = createResourceFromInput({
    name: "LegacyCleanup",
    displayName: "历史清理微流",
    description: "归档状态资源，用于权限与恢复流程验收。",
    moduleId: "ops",
    moduleName: "Ops",
    tags: ["archived"],
    parameters: [],
    returnType: { kind: "void" },
    template: "blank",
  }, options, {
    id: "mf-legacy-cleanup",
    status: "archived",
    archived: true,
    publishStatus: "changedAfterPublish",
    permissions: { ...defaultMockPermissions(), canPublish: false },
  });

  const rest = createResourceFromInput({
    name: "RestErrorHandlingSample",
    displayName: "REST 错误处理样例",
    description: "包含 REST 调用和错误线的契约 mock 样例。",
    moduleId: "integration",
    moduleName: "Integration",
    tags: ["rest", "error"],
    parameters: [],
    returnType: { kind: "void" },
    template: "restErrorHandling",
  }, options, { id: "mf-rest-error-handling" });

  const loop = createResourceFromInput({
    name: "LoopProcessingSample",
    displayName: "循环处理样例",
    description: "包含 looped activity 的契约 mock 样例。",
    moduleId: "sales",
    moduleName: "Sales",
    tags: ["loop", "list"],
    parameters: [],
    returnType: { kind: "void" },
    template: "loopProcessing",
  }, options, { id: "mf-loop-processing" });

  const objectDecision = createResourceFromInput({
    name: "ObjectTypeDecisionSample",
    displayName: "对象类型分支样例",
    description: "包含对象类型判断的契约 mock 样例。",
    moduleId: "sales",
    moduleName: "Sales",
    tags: ["object-type", "decision"],
    parameters: [],
    returnType: { kind: "void" },
    template: "objectTypeDecision",
  }, options, { id: "mf-object-type-decision" });

  return [published, changedAfterPublish, draft, archived, rest, loop, objectDecision];
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

  const resolved: Required<MicroflowContractMockSeedOptions> = {
    workspaceId: options.workspaceId ?? "default-workspace",
    currentUser: options.currentUser ?? { id: "current-user", name: "Current User" },
  };

  for (const resource of seedResourceSet(resolved)) {
    saveMockResource(store, resource);
    if (resource.latestPublishedVersion) {
      const baselineSchema = resource.id === "mf-order-breaking"
        ? { ...clone(store.resources.get("mf-order-process")?.schema ?? resource.schema), id: resource.id, stableId: resource.id }
        : resource.schema;
      const snapshot = createMockPublishSnapshot(resource.id, resource.latestPublishedVersion, baselineSchema, {
        publishedAt: resource.lastRunAt ?? resource.updatedAt,
        publishedBy: resolved.currentUser.name,
        description: "Initial published version.",
      });
      store.publishSnapshots.set(snapshot.id, snapshot);
      store.versions.set(resource.id, [createMockVersionSummary(snapshot, { createdBy: resolved.currentUser.name, referenceCount: resource.referenceCount })]);
    } else {
      store.versions.set(resource.id, [{
        id: `version-${resource.id}-draft`,
        resourceId: resource.id,
        version: resource.version,
        status: "draft",
        createdAt: resource.createdAt,
        createdBy: resource.createdBy,
        description: resource.description,
        schemaSnapshotId: `${resource.id}@draft`,
        validationSummary: summarizeValidation(resource.schema, store.metadataCatalog),
        referenceCount: resource.referenceCount,
        isLatestPublished: false,
      }]);
    }
    store.references.set(resource.id, resource.referenceCount > 0 ? createMockReferences(resource.id, resource.latestPublishedVersion) : []);
  }
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
  const normalized = { ...resource, schema: normalizeMicroflowSchema(clone(resource.schema) as unknown) };
  store.resources.set(resource.id, clone(normalized));
  store.schemaSnapshots.set(resource.id, createSchemaSnapshot(normalized));
  return clone(normalized);
}

export function createMockSchemaSnapshot(store: MicroflowContractMockStore, resourceId: string, schema: MicroflowAuthoringSchema): MicroflowSchemaSnapshotContract {
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

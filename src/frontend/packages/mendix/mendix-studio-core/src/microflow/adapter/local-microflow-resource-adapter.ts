import { normalizeMicroflowSchema, validateMicroflowSchema, type MicroflowAuthoringSchema } from "@atlas/microflow";
import { getDefaultMockMetadataCatalog } from "@atlas/microflow/metadata";

import type { MicroflowPublishInput, MicroflowPublishResult } from "../publish/microflow-publish-types";
import { analyzeMicroflowPublishImpact, hashSchemaSnapshot, summarizeValidation, validatePublishVersion } from "../publish/microflow-publish-utils";
import type { MicroflowReference } from "../references/microflow-reference-types";
import { createDefaultMicroflowSchema } from "../schema/create-default-microflow-schema";
import { diffMicroflowSchemas } from "../versions/microflow-version-diff";
import type { MicroflowPublishedSnapshot, MicroflowVersionDetail, MicroflowVersionDiff, MicroflowVersionSummary } from "../versions/microflow-version-types";
import type { GetMicroflowReferencesRequest } from "../contracts/api/microflow-reference-api-contract";
import type { MicroflowResourceAdapter, SaveMicroflowSchemaOptions } from "./microflow-resource-adapter";
import { readStoredMicroflowResources, writeStoredMicroflowResources, type StoredMicroflowResources } from "./microflow-resource-storage";
import type {
  MicroflowCreateInput,
  MicroflowDuplicateInput,
  MicroflowPublishStatus,
  MicroflowResource,
  MicroflowResourceListResult,
  MicroflowResourcePatch,
  MicroflowResourceQuery
} from "../resource/resource-types";

export interface LocalMicroflowResourceAdapterOptions {
  workspaceId?: string;
  currentUser?: { id: string; name: string };
  storageKey?: string;
  enableLocalStorage?: boolean;
}

function nowIso(): string {
  return new Date().toISOString();
}

function makeId(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
}

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

function normalizeResource(resource: MicroflowResource): MicroflowResource {
  return {
    ...resource,
    schema: normalizeMicroflowSchema(resource.schema as unknown)
  };
}

function normalizeSnapshot(snapshot: MicroflowPublishedSnapshot): MicroflowPublishedSnapshot {
  return {
    ...snapshot,
    schema: normalizeMicroflowSchema(snapshot.schema as unknown)
  };
}

function defaultPermissions() {
  return {
    canEdit: true,
    canDelete: true,
    canPublish: true,
    canArchive: true,
    canDuplicate: true
  };
}

function normalizeVersion(value: string): string {
  return value.trim() || "0.1.0";
}

function nextPatchVersion(version: string): string {
  const [core] = version.replace(/^v/u, "").split("-");
  const parts = core.split(".").map(part => Number(part));
  if (parts.length === 0 || parts.some(part => !Number.isFinite(part))) {
    return "1.0.0";
  }
  while (parts.length < 3) {
    parts.push(0);
  }
  parts[2] += 1;
  return parts.join(".");
}

function createValidationSummary(schema: MicroflowAuthoringSchema) {
  return summarizeValidation(schema);
}

function createSnapshot(input: {
  id: string;
  resourceId: string;
  version: string;
  schema: MicroflowAuthoringSchema;
  publishedAt: string;
  publishedBy?: string;
  description?: string;
}): MicroflowPublishedSnapshot {
  const schema = clone(input.schema);
  return {
    id: input.id,
    resourceId: input.resourceId,
    version: input.version,
    schema,
    publishedAt: input.publishedAt,
    publishedBy: input.publishedBy,
    description: input.description,
    validationSummary: createValidationSummary(schema),
    schemaHash: hashSchemaSnapshot(schema)
  };
}

function createResourceFromInput(input: MicroflowCreateInput, options: Required<Pick<LocalMicroflowResourceAdapterOptions, "workspaceId" | "currentUser">>): MicroflowResource {
  const id = makeId("mf");
  const timestamp = nowIso();
  const schema = createDefaultMicroflowSchema({ ...input, id, ownerName: options.currentUser.name });
  return {
    id,
    schemaId: schema.id,
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
    updatedAt: timestamp,
    version: schema.audit.version,
    status: "draft",
    publishStatus: "neverPublished",
    favorite: false,
    archived: false,
    referenceCount: 0,
    lastRunStatus: "neverRun",
    schema,
    permissions: defaultPermissions()
  };
}

function seedResources(options: Required<Pick<LocalMicroflowResourceAdapterOptions, "workspaceId" | "currentUser">>): StoredMicroflowResources {
  const order = createResourceFromInput({
    name: "OrderProcessing",
    displayName: "订单处理微流",
    description: "处理订单校验、库存检查与提交的示例微流。",
    moduleId: "sales",
    moduleName: "Sales",
    tags: ["order", "inventory", "published"],
    parameters: [
      { id: "param-order-id", stableId: "param-order-id", name: "orderId", dataType: { kind: "string" }, required: true, documentation: "Order identifier." }
    ],
    returnType: { kind: "boolean" },
    returnVariableName: "isProcessed",
    template: "orderProcessing"
  }, options);
  const publishedAt = nowIso();
  const published: MicroflowResource = {
    ...order,
    id: "mf-order-process",
    schemaId: "mf-order-process",
    status: "published",
    publishStatus: "published",
    latestPublishedVersion: "1.0.0",
    version: "1.0.0",
    favorite: true,
    referenceCount: 3,
    lastRunStatus: "success",
    lastRunAt: publishedAt,
    schema: { ...order.schema, id: "mf-order-process", stableId: "mf-order-process", audit: { ...order.schema.audit, status: "published", version: "1.0.0" } }
  };
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
    status: "published",
    publishStatus: "changedAfterPublish",
    latestPublishedVersion: "1.0.0",
    referenceCount: 2,
    favorite: false,
    schema: changedSchema
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
    template: "blank"
  }, options);
  const publishedSnapshotId = `${published.id}@1.0.0`;
  const changedSnapshotId = `${changedAfterPublish.id}@1.0.0`;
  const publishedSnapshot = createSnapshot({
    id: publishedSnapshotId,
    resourceId: published.id,
    version: "1.0.0",
    schema: published.schema,
    publishedAt,
    publishedBy: options.currentUser.name,
    description: "Initial published version."
  });
  const changedSnapshotSchema = clone(published.schema);
  changedSnapshotSchema.id = changedAfterPublish.id;
  changedSnapshotSchema.stableId = changedAfterPublish.id;
  const changedSnapshot = createSnapshot({
    id: changedSnapshotId,
    resourceId: changedAfterPublish.id,
    version: "1.0.0",
    schema: changedSnapshotSchema,
    publishedAt,
    publishedBy: options.currentUser.name,
    description: "Baseline before breaking draft changes."
  });
  return {
    resources: [published, changedAfterPublish, draft],
    versions: {
      [published.id]: [
        {
          id: makeId("version"),
          resourceId: published.id,
          version: "1.0.0",
          status: "published",
          createdAt: publishedAt,
          createdBy: options.currentUser.name,
          description: "Initial published version.",
          schemaSnapshotId: publishedSnapshotId,
          validationSummary: publishedSnapshot.validationSummary,
          referenceCount: published.referenceCount,
          isLatestPublished: true
        }
      ],
      [changedAfterPublish.id]: [
        {
          id: makeId("version"),
          resourceId: changedAfterPublish.id,
          version: "1.0.0",
          status: "published",
          createdAt: publishedAt,
          createdBy: options.currentUser.name,
          description: "Baseline before breaking draft changes.",
          schemaSnapshotId: changedSnapshotId,
          validationSummary: changedSnapshot.validationSummary,
          referenceCount: changedAfterPublish.referenceCount,
          isLatestPublished: true
        }
      ]
    },
    snapshots: {
      [publishedSnapshotId]: publishedSnapshot,
      [changedSnapshotId]: changedSnapshot
    },
    references: {
      [published.id]: createMockReferences(published.id, "1.0.0"),
      [changedAfterPublish.id]: createMockReferences(changedAfterPublish.id, "1.0.0").slice(0, 2),
      [draft.id]: []
    }
  };
}

function createDraftVersion(resource: MicroflowResource): MicroflowVersionSummary {
  return {
    id: makeId("version"),
    resourceId: resource.id,
    version: resource.version,
    status: "draft",
    createdAt: resource.createdAt,
    createdBy: resource.createdBy,
    description: resource.description,
    schemaSnapshotId: `${resource.id}@draft-${resource.createdAt}`,
    validationSummary: createValidationSummary(resource.schema),
    referenceCount: resource.referenceCount,
    isLatestPublished: false
  };
}

function createMockReferences(resourceId: string, referencedVersion?: string): MicroflowReference[] {
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
      description: "另一个微流将该资源作为可复用动作调用。"
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
      description: "页面按钮点击后触发该微流。"
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
      description: "工作流活动依赖当前发布契约。"
    }
  ];
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

export class LocalMicroflowResourceAdapter implements MicroflowResourceAdapter {
  private resources = new Map<string, MicroflowResource>();
  private versions = new Map<string, MicroflowVersionSummary[]>();
  private snapshots = new Map<string, MicroflowPublishedSnapshot>();
  private references = new Map<string, MicroflowReference[]>();
  private readonly workspaceId: string;
  private readonly currentUser: { id: string; name: string };
  private readonly storageKey?: string;
  private readonly enableLocalStorage: boolean;

  constructor(options: LocalMicroflowResourceAdapterOptions = {}) {
    this.workspaceId = options.workspaceId || "default-workspace";
    this.currentUser = options.currentUser || { id: "current-user", name: "Current User" };
    this.storageKey = options.storageKey;
    this.enableLocalStorage = options.enableLocalStorage !== false;
    // local development only: persistence is opt-out so tests can run in-memory.
    const restored = (this.enableLocalStorage ? readStoredMicroflowResources(this.storageKey) : undefined) ?? seedResources({ workspaceId: this.workspaceId, currentUser: this.currentUser });
    restored.resources.forEach(resource => this.resources.set(resource.id, clone(normalizeResource(resource))));
    Object.entries(restored.versions ?? {}).forEach(([id, versions]) => this.versions.set(id, clone(versions)));
    Object.entries(restored.snapshots ?? {}).forEach(([id, snapshot]) => this.snapshots.set(id, clone(normalizeSnapshot(snapshot))));
    Object.entries(restored.references ?? {}).forEach(([id, references]) => this.references.set(id, clone(references)));
    this.resources.forEach(resource => {
      if (!this.versions.has(resource.id)) {
        this.versions.set(resource.id, [createDraftVersion(resource)]);
      }
      if (!this.references.has(resource.id)) {
        this.references.set(resource.id, resource.referenceCount > 0 ? createMockReferences(resource.id, resource.latestPublishedVersion) : []);
      }
    });
    this.persist();
  }

  async listMicroflows(query: MicroflowResourceQuery = {}): Promise<MicroflowResourceListResult> {
    const keyword = query.keyword?.trim().toLowerCase();
    const tags = query.tags ?? [];
    const sortBy = query.sortBy ?? "updatedAt";
    const sortOrder = query.sortOrder ?? "desc";
    const pageIndex = query.pageIndex;
    const pageSize = query.pageSize;
    const items = [...this.resources.values()]
      .filter(resource => (query.workspaceId ? resource.workspaceId === query.workspaceId : true))
      .filter(resource => !query.status?.length || query.status.includes(resource.status))
      .filter(resource => {
        if (!query.publishStatus?.length) {
          return true;
        }
        const ps: MicroflowPublishStatus = resource.publishStatus ?? "neverPublished";
        return query.publishStatus!.includes(ps);
      })
      .filter(resource => !query.favoriteOnly || resource.favorite)
      .filter(resource => !query.ownerId || resource.ownerId === query.ownerId || resource.createdBy === query.ownerId)
      .filter(resource => !query.moduleId || resource.moduleId === query.moduleId)
      .filter(resource => !tags.length || tags.some(tag => resource.tags.includes(tag)))
      .filter(resource => isInRange(resource.updatedAt, query.updatedFrom, query.updatedTo))
      .filter(resource => {
        if (!keyword) {
          return true;
        }
        return [resource.name, resource.displayName, resource.description, ...resource.tags].filter(Boolean).join(" ").toLowerCase().includes(keyword);
      })
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
    const total = items.length;
    if (pageIndex != null && pageIndex >= 1 && pageSize != null && pageSize > 0) {
      const start = (pageIndex - 1) * pageSize;
      const page = items.slice(start, start + pageSize);
      return {
        items: clone(page),
        total,
        pageIndex,
        pageSize,
        hasMore: start + page.length < total
      };
    }
    return { items: clone(items), total };
  }

  async getMicroflow(id: string): Promise<MicroflowResource | undefined> {
    const resource = this.resources.get(id);
    return resource ? clone(resource) : undefined;
  }

  async createMicroflow(input: MicroflowCreateInput): Promise<MicroflowResource> {
    const resource = createResourceFromInput(input, { workspaceId: this.workspaceId, currentUser: this.currentUser });
    this.resources.set(resource.id, resource);
    this.versions.set(resource.id, [createDraftVersion(resource)]);
    this.references.set(resource.id, []);
    this.persist();
    return clone(resource);
  }

  async updateMicroflow(id: string, patch: MicroflowResourcePatch): Promise<MicroflowResource> {
    const current = this.requireResource(id);
    const timestamp = nowIso();
    const schema = patch.schema ? normalizeMicroflowSchema(clone(patch.schema) as unknown) : current.schema;
    const next: MicroflowResource = {
      ...current,
      ...patch,
      schema,
      publishStatus: patch.publishStatus ?? current.publishStatus,
      updatedAt: timestamp,
      updatedBy: this.currentUser.name
    };
    this.resources.set(id, next);
    this.persist();
    return clone(next);
  }

  async saveMicroflowSchema(id: string, schema: MicroflowAuthoringSchema, _options?: SaveMicroflowSchemaOptions): Promise<MicroflowResource> {
    void _options;
    const current = this.requireResource(id);
    const timestamp = nowIso();
    const nextSchema = normalizeMicroflowSchema(clone(schema) as unknown);
    nextSchema.id = current.schemaId;
    nextSchema.audit = {
      ...nextSchema.audit,
      version: current.version,
      status: current.status === "published" ? "draft" : nextSchema.audit.status,
      updatedAt: timestamp,
      updatedBy: this.currentUser.name
    };
    const next: MicroflowResource = {
      ...current,
      name: nextSchema.name,
      displayName: nextSchema.displayName || current.displayName,
      description: nextSchema.description,
      schema: nextSchema,
      status: current.status === "archived" ? "archived" : current.status,
      publishStatus: current.latestPublishedVersion ? "changedAfterPublish" : "neverPublished",
      updatedAt: timestamp,
      updatedBy: this.currentUser.name
    };
    this.resources.set(id, next);
    this.persist();
    return clone(next);
  }

  async duplicateMicroflow(id: string, input: MicroflowDuplicateInput = {}): Promise<MicroflowResource> {
    const current = this.requireResource(id);
    const nextId = makeId("mf");
    const timestamp = nowIso();
    const schema = clone(current.schema);
    schema.id = nextId;
    schema.stableId = nextId;
    schema.name = input.name || `${current.name}Copy`;
    schema.displayName = input.displayName || `${current.displayName || current.name} Copy`;
    schema.moduleId = input.moduleId || current.moduleId;
    schema.moduleName = input.moduleName || current.moduleName;
    schema.audit = { version: "0.1.0", status: "draft", createdAt: timestamp, createdBy: this.currentUser.name, updatedAt: timestamp, updatedBy: this.currentUser.name };
    const duplicate: MicroflowResource = {
      ...current,
      id: nextId,
      schemaId: nextId,
      name: schema.name,
      displayName: schema.displayName,
      moduleId: schema.moduleId,
      moduleName: schema.moduleName,
      tags: input.tags ?? [...current.tags],
      createdAt: timestamp,
      createdBy: this.currentUser.name,
      updatedAt: timestamp,
      updatedBy: this.currentUser.name,
      version: "0.1.0",
      latestPublishedVersion: undefined,
      status: "draft",
      publishStatus: "neverPublished",
      favorite: false,
      archived: false,
      referenceCount: 0,
      schema,
      permissions: defaultPermissions()
    };
    this.resources.set(nextId, duplicate);
    this.versions.set(nextId, [createDraftVersion(duplicate)]);
    this.references.set(nextId, []);
    this.persist();
    return clone(duplicate);
  }

  async renameMicroflow(id: string, name: string, displayName?: string): Promise<MicroflowResource> {
    const current = this.requireResource(id);
    return this.updateMicroflow(id, {
      name,
      displayName: displayName || name,
      schema: { ...current.schema, name, displayName: displayName || name }
    });
  }

  async toggleFavorite(id: string, favorite: boolean): Promise<MicroflowResource> {
    return this.updateMicroflow(id, { favorite });
  }

  async archiveMicroflow(id: string): Promise<MicroflowResource> {
    const current = this.requireResource(id);
    return this.updateMicroflow(id, { status: "archived", archived: true, permissions: { ...(current.permissions ?? defaultPermissions()), canPublish: false } });
  }

  async restoreMicroflow(id: string): Promise<MicroflowResource> {
    return this.updateMicroflow(id, { status: "draft", archived: false, publishStatus: "changedAfterPublish", permissions: defaultPermissions() });
  }

  async deleteMicroflow(id: string): Promise<void> {
    this.resources.delete(id);
    this.versions.delete(id);
    this.references.delete(id);
    [...this.snapshots.entries()]
      .filter(([, snapshot]) => snapshot.resourceId === id)
      .forEach(([snapshotId]) => this.snapshots.delete(snapshotId));
    this.persist();
  }

  async publishMicroflow(id: string, input: MicroflowPublishInput): Promise<MicroflowPublishResult> {
    const current = this.requireResource(id);
    if (current.archived) {
      throw new Error("Archived microflows cannot be published.");
    }
    const existingVersions = this.versions.get(id) ?? [];
    const version = normalizeVersion(input.version || nextPatchVersion(current.version));
    const versionValidation = validatePublishVersion(version, existingVersions);
    if (!versionValidation.valid) {
      throw new Error(versionValidation.message);
    }
    const validation = validateMicroflowSchema({
      schema: current.schema,
      metadata: getDefaultMockMetadataCatalog(),
      options: { mode: "publish", includeWarnings: true, includeInfo: true },
    });
    if (validation.summary.errorCount > 0) {
      throw new Error("存在错误，无法发布。");
    }
    const impactAnalysis = await this.analyzeMicroflowPublishImpact(id, { ...input, version });
    if (impactAnalysis.summary.highImpactCount > 0 && !input.confirmBreakingChanges && !input.force) {
      throw new Error("存在高影响破坏性变更，发布前需要二次确认。");
    }
    const timestamp = nowIso();
    const description = input.description ?? input.releaseNote;
    const schema: MicroflowAuthoringSchema = {
      ...clone(current.schema),
      audit: { ...current.schema.audit, status: "published", version, updatedAt: timestamp, updatedBy: this.currentUser.name }
    };
    const snapshotId = `${id}@${version}`;
    const snapshot = createSnapshot({
      id: snapshotId,
      resourceId: id,
      version,
      schema,
      publishedAt: timestamp,
      publishedBy: this.currentUser.name,
      description
    });
    const next: MicroflowResource = {
      ...current,
      status: "published",
      publishStatus: "published",
      latestPublishedVersion: version,
      version,
      updatedAt: timestamp,
      updatedBy: this.currentUser.name,
      schema
    };
    this.resources.set(id, next);
    this.snapshots.set(snapshotId, snapshot);
    const versionSummary: MicroflowVersionSummary = {
      id: makeId("version"),
      resourceId: id,
      version,
      status: "published",
      createdAt: timestamp,
      createdBy: this.currentUser.name,
      description,
      schemaSnapshotId: snapshotId,
      validationSummary: validation.summary,
      referenceCount: this.references.get(id)?.length ?? next.referenceCount,
      isLatestPublished: true
    };
    this.versions.set(id, [
      versionSummary,
      ...existingVersions.map(item => ({ ...item, isLatestPublished: false }))
    ]);
    this.persist();
    return {
      resource: clone(next),
      version: clone(versionSummary),
      snapshot: clone(snapshot),
      validationSummary: validation.summary,
      impactAnalysis
    };
  }

  async getMicroflowReferences(id: string, query?: GetMicroflowReferencesRequest): Promise<MicroflowReference[]> {
    const current = this.requireResource(id);
    const list = this.references.get(id) ?? (current.referenceCount > 0 ? createMockReferences(id, current.latestPublishedVersion) : []);
    let filtered = list.filter(ref => (query?.includeInactive ? true : ref.active !== false));
    if (query?.sourceType?.length) {
      filtered = filtered.filter(r => query.sourceType!.includes(r.sourceType));
    }
    if (query?.impactLevel?.length) {
      filtered = filtered.filter(r => query.impactLevel!.includes(r.impactLevel));
    }
    return clone(filtered);
  }

  async getMicroflowVersions(id: string): Promise<MicroflowVersionSummary[]> {
    this.requireResource(id);
    return clone(this.versions.get(id) ?? []);
  }

  async getMicroflowVersionDetail(id: string, versionId: string): Promise<MicroflowVersionDetail | undefined> {
    const current = this.requireResource(id);
    const version = (this.versions.get(id) ?? []).find(item => item.id === versionId);
    if (!version) {
      return undefined;
    }
    const snapshot = this.snapshots.get(version.schemaSnapshotId);
    if (!snapshot) {
      return undefined;
    }
    return clone({
      ...version,
      snapshot,
      diffFromCurrent: diffMicroflowSchemas(snapshot.schema, current.schema)
    });
  }

  async rollbackMicroflowVersion(id: string, versionId: string): Promise<MicroflowResource> {
    const current = this.requireResource(id);
    const detail = await this.getMicroflowVersionDetail(id, versionId);
    if (!detail) {
      throw new Error("Version snapshot was not found.");
    }
    const timestamp = nowIso();
    const schema = normalizeMicroflowSchema(clone(detail.snapshot.schema) as unknown);
    schema.audit = { ...schema.audit, version: current.version, status: "draft", updatedAt: timestamp, updatedBy: this.currentUser.name };
    const next: MicroflowResource = {
      ...current,
      name: schema.name,
      displayName: schema.displayName,
      description: schema.description,
      schema,
      status: "draft",
      publishStatus: current.latestPublishedVersion ? "changedAfterPublish" : "neverPublished",
      updatedAt: timestamp,
      updatedBy: this.currentUser.name
    };
    this.resources.set(id, next);
    this.versions.set(id, [
      {
        id: makeId("version"),
        resourceId: id,
        version: detail.version,
        status: "rolledBack",
        createdAt: timestamp,
        createdBy: this.currentUser.name,
        description: `Rolled back from ${detail.version}`,
        schemaSnapshotId: detail.schemaSnapshotId,
        validationSummary: detail.validationSummary,
        referenceCount: detail.referenceCount,
        isLatestPublished: false
      },
      ...(this.versions.get(id) ?? [])
    ]);
    this.persist();
    return clone(next);
  }

  async duplicateMicroflowVersion(id: string, versionId: string, input: MicroflowDuplicateInput = {}): Promise<MicroflowResource> {
    const current = this.requireResource(id);
    const detail = await this.getMicroflowVersionDetail(id, versionId);
    if (!detail) {
      throw new Error("Version snapshot was not found.");
    }
    const nextId = makeId("mf");
    const timestamp = nowIso();
    const schema = normalizeMicroflowSchema(clone(detail.snapshot.schema) as unknown);
    schema.id = nextId;
    schema.stableId = nextId;
    schema.name = input.name || `${current.name}VersionCopy`;
    schema.displayName = input.displayName || `${current.displayName || current.name} ${detail.version} Copy`;
    schema.moduleId = input.moduleId || current.moduleId;
    schema.moduleName = input.moduleName || current.moduleName;
    schema.audit = { ...schema.audit, version: "0.1.0", status: "draft", createdAt: timestamp, createdBy: this.currentUser.name, updatedAt: timestamp, updatedBy: this.currentUser.name };
    const duplicate: MicroflowResource = {
      ...current,
      id: nextId,
      schemaId: nextId,
      name: schema.name,
      displayName: schema.displayName,
      moduleId: schema.moduleId,
      moduleName: schema.moduleName,
      tags: input.tags ?? [...current.tags],
      createdAt: timestamp,
      createdBy: this.currentUser.name,
      updatedAt: timestamp,
      updatedBy: this.currentUser.name,
      version: "0.1.0",
      latestPublishedVersion: undefined,
      status: "draft",
      publishStatus: "neverPublished",
      favorite: false,
      archived: false,
      referenceCount: 0,
      schema,
      permissions: defaultPermissions()
    };
    this.resources.set(nextId, duplicate);
    this.versions.set(nextId, [createDraftVersion(duplicate)]);
    this.references.set(nextId, []);
    this.persist();
    return clone(duplicate);
  }

  async analyzeMicroflowPublishImpact(id: string, input: MicroflowPublishInput) {
    const current = this.requireResource(id);
    const latestSnapshot = [...this.snapshots.values()]
      .filter(snapshot => snapshot.resourceId === id && snapshot.version === current.latestPublishedVersion)
      .sort((left, right) => right.publishedAt.localeCompare(left.publishedAt))[0];
    return analyzeMicroflowPublishImpact({
      resourceId: id,
      currentVersion: current.latestPublishedVersion,
      nextVersion: input.version,
      currentSchema: current.schema,
      latestSnapshot,
      references: await this.getMicroflowReferences(id)
    });
  }

  async compareMicroflowVersion(id: string, versionId: string): Promise<MicroflowVersionDiff> {
    const current = this.requireResource(id);
    const detail = await this.getMicroflowVersionDetail(id, versionId);
    if (!detail) {
      throw new Error("Version snapshot was not found.");
    }
    return diffMicroflowSchemas(detail.snapshot.schema, current.schema);
  }

  private requireResource(id: string): MicroflowResource {
    const resource = this.resources.get(id);
    if (!resource) {
      throw new Error(`Microflow ${id} was not found.`);
    }
    return clone(resource);
  }

  private persist(): void {
    if (!this.enableLocalStorage) {
      return;
    }
    writeStoredMicroflowResources({
      resources: [...this.resources.values()],
      versions: Object.fromEntries(this.versions.entries()),
      snapshots: Object.fromEntries(this.snapshots.entries()),
      references: Object.fromEntries(this.references.entries())
    }, this.storageKey);
  }
}

export function createLocalMicroflowResourceAdapter(options?: LocalMicroflowResourceAdapterOptions): MicroflowResourceAdapter {
  return new LocalMicroflowResourceAdapter(options);
}

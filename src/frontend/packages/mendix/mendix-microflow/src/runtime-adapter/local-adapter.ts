/**
 * Local development/offline debug only.
 * Do not use localStorage adapter as production persistence.
 */
import { validateMicroflowSchema } from "../schema/validator";
import { ensureAuthoringSchema, flattenObjectCollection } from "../adapters";
import { getDefaultMockMetadataCatalog } from "../metadata";
import { buildVariableIndex } from "../variables";
import { mockTestRunMicroflow } from "../debug";
import type {
  CreateMicroflowInput,
  MicroflowListQuery,
  MicroflowReference,
  MicroflowResource,
  MicroflowResourceSortKey,
  MicroflowResourceStatus,
  MicroflowDataType,
  MicroflowTypeRef,
  MicroflowAuthoringSchema,
  PublishMicroflowPayload
} from "../schema/types";
import { sampleMicroflowSchema } from "../schema/sample";
import type {
  ListMicroflowRunsResponse,
  MicroflowApiClient,
  MicroflowRunHistoryQuery,
  MicroflowRunHistoryStatus,
  MicroflowTraceFrame,
  MicroflowRunSession,
  PublishMicroflowResponse,
  SaveMicroflowRequest,
  SaveMicroflowResponse,
  TestRunMicroflowRequest,
  TestRunMicroflowResponse,
  ValidateMicroflowRequest,
  ValidateMicroflowResponse
} from "./types";

const STORAGE_KEY = "atlas_microflow_resources_v1";

function cloneSchema(schema: MicroflowAuthoringSchema): MicroflowAuthoringSchema {
  const cloned = JSON.parse(JSON.stringify(schema)) as MicroflowAuthoringSchema;
  return ensureAuthoringSchema(cloned);
}

function cloneResource(resource: MicroflowResource): MicroflowResource {
  return {
    ...resource,
    tags: [...resource.tags],
    schema: cloneSchema(resource.schema)
  };
}

function typeRefToDataType(type: MicroflowTypeRef): MicroflowDataType {
  if (type.kind === "void") {
    return { kind: "void" };
  }
  if (type.kind === "entity" || type.kind === "object") {
    return { kind: "object", entityQualifiedName: type.entity ?? type.name };
  }
  if (type.kind === "list") {
    return { kind: "list", itemType: type.itemType ? typeRefToDataType(type.itemType) : { kind: "unknown", reason: "missing list item type" } };
  }
  if (type.name === "Boolean") {
    return { kind: "boolean" };
  }
  if (type.name === "Integer") {
    return { kind: "integer" };
  }
  if (type.name === "Long") {
    return { kind: "long" };
  }
  if (type.name === "Decimal") {
    return { kind: "decimal" };
  }
  if (type.name === "DateTime") {
    return { kind: "dateTime" };
  }
  if (type.name === "String" || type.kind === "primitive") {
    return { kind: "string" };
  }
  return { kind: "unknown", reason: type.name };
}

function nowIso(): string {
  return new Date().toISOString();
}

function compareByString(a: string, b: string): number {
  return a.localeCompare(b, undefined, { numeric: true, sensitivity: "base" });
}

function updatedRangeMatches(resource: MicroflowResource, range: NonNullable<MicroflowListQuery["updatedRange"]>): boolean {
  if (range === "all") {
    return true;
  }
  const updated = new Date(resource.updatedAt).getTime();
  if (Number.isNaN(updated)) {
    return false;
  }
  const ageMs = Date.now() - updated;
  if (range === "today") {
    return ageMs <= 24 * 60 * 60 * 1000;
  }
  if (range === "week") {
    return ageMs <= 7 * 24 * 60 * 60 * 1000;
  }
  return ageMs <= 30 * 24 * 60 * 60 * 1000;
}

function makeResource(
  id: string,
  overrides: Partial<Omit<MicroflowResource, "id" | "schema">> & { schema?: MicroflowAuthoringSchema }
): MicroflowResource {
  const timestamp = nowIso();
  const schema = cloneSchema(overrides.schema ?? sampleMicroflowSchema);
  schema.id = id;
  schema.name = overrides.name ?? schema.name;
  schema.description = overrides.description ?? schema.description;
  const resourceVersion = overrides.version ?? schema.audit.version;
  schema.audit = { ...schema.audit, version: resourceVersion };
  return {
    id,
    name: schema.name,
    description: schema.description ?? "",
    moduleId: "order",
    moduleName: "Order",
    ownerName: "Admin",
    sharedWithMe: false,
    tags: ["order", "demo"],
    version: resourceVersion,
    status: "draft",
    favorite: false,
    createdAt: timestamp,
    updatedAt: timestamp,
    lastModifiedBy: "Admin",
    ...overrides,
    schema
  };
}

function defaultResources(): MicroflowResource[] {
  const publishedAt = nowIso();
  return [
    makeResource("mf-order-process", {
      name: "Order Processing Microflow",
      description: "Sample microflow for order retrieval, status validation, inventory call, and commit.",
      version: "v3",
      status: "published",
      favorite: true,
      publishedAt,
      tags: ["order", "inventory", "published"]
    }),
    makeResource("mf-customer-onboarding", {
      name: "Customer Onboarding Microflow",
      description: "Creates a customer profile and calls an external verification service.",
      moduleId: "customer",
      moduleName: "Customer",
      version: "v1",
      status: "draft",
      tags: ["customer", "draft"]
    }),
    makeResource("mf-payment-archive", {
      name: "Payment Archive Microflow",
      description: "Archives payment records and writes audit logs.",
      moduleId: "payment",
      moduleName: "Payment",
      version: "v2",
      status: "archived",
      sharedWithMe: true,
      tags: ["payment", "archive"]
    })
  ];
}

export class LocalMicroflowApiClient implements MicroflowApiClient {
  private readonly resources = new Map<string, MicroflowResource>();
  private readonly traces = new Map<string, MicroflowTraceFrame[]>();
  private readonly sessions = new Map<string, MicroflowRunSession>();
  private readonly runMicroflowIndex = new Map<string, string>();

  constructor(initialSchemas: MicroflowAuthoringSchema[] = [sampleMicroflowSchema]) {
    const restored = this.restoreResources();
    if (restored.length > 0) {
      for (const resource of restored) {
        this.resources.set(resource.id, cloneResource(resource));
      }
      return;
    }

    if (initialSchemas.length === 1 && initialSchemas[0]?.id === sampleMicroflowSchema.id) {
      for (const resource of defaultResources()) {
        this.resources.set(resource.id, resource);
      }
    } else {
      for (const schema of initialSchemas) {
        this.resources.set(schema.id, makeResource(schema.id, { schema, name: schema.name, description: schema.description ?? "" }));
      }
    }
    this.persistResources();
  }

  async listMicroflows(query: MicroflowListQuery = {}): Promise<MicroflowResource[]> {
    const keyword = query.keyword?.trim().toLowerCase();
    const status = query.status && query.status !== "all" ? query.status : undefined;
    const tag = query.tag?.trim();
    const ownerName = query.ownerName?.trim();
    const updatedRange = query.updatedRange ?? "all";
    const sortBy = query.sortBy ?? "updatedAt";

    const items = [...this.resources.values()]
      .filter(resource => {
        if (query.scope === "mine" && resource.sharedWithMe) {
          return false;
        }
        if (query.scope === "shared" && !resource.sharedWithMe) {
          return false;
        }
        if (query.scope === "favorite" && !resource.favorite) {
          return false;
        }
        if (status && resource.status !== status) {
          return false;
        }
        if (tag && !resource.tags.includes(tag)) {
          return false;
        }
        if (ownerName && resource.ownerName !== ownerName) {
          return false;
        }
        if (!updatedRangeMatches(resource, updatedRange)) {
          return false;
        }
        if (!keyword) {
          return true;
        }
        const haystack = [resource.name, resource.description, resource.moduleName, ...resource.tags].join(" ").toLowerCase();
        return haystack.includes(keyword);
      })
      .sort((a, b) => this.compareResources(a, b, sortBy));

    return items.map(cloneResource);
  }

  async createMicroflow(input: CreateMicroflowInput): Promise<MicroflowResource> {
    const id = `mf-${Date.now()}`;
    const schema = cloneSchema(sampleMicroflowSchema);
    schema.id = id;
    schema.name = input.name.trim();
    schema.description = input.description.trim();
    schema.audit = { ...schema.audit, version: "v0.1" };
    if (input.returnType?.kind && input.returnType.kind !== "void") {
      const returnType = typeRefToDataType(input.returnType);
      schema.returnType = returnType;
      schema.objectCollection.objects = schema.objectCollection.objects.map(object => object.kind === "endEvent"
        ? {
            ...object,
            returnValue: {
              id: `${id}-return`,
              raw: "empty",
              inferredType: returnType,
              references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
              diagnostics: []
            }
          }
        : object);
    }
    const resource = makeResource(id, {
      schema,
      name: input.name.trim(),
      description: input.description.trim(),
      moduleId: input.moduleId.trim() || "default",
      moduleName: input.moduleName?.trim() || input.moduleId.trim() || "Default",
      tags: input.tags,
      version: schema.audit.version,
      status: "draft",
      favorite: false
    });
    this.resources.set(id, resource);
    this.persistResources();
    return cloneResource(resource);
  }

  async getMicroflow(id: string): Promise<MicroflowResource> {
    const resource = this.resources.get(id);
    if (!resource) {
      throw new Error(`Microflow ${id} was not found.`);
    }
    return cloneResource(resource);
  }

  async saveMicroflow(request: SaveMicroflowRequest): Promise<SaveMicroflowResponse> {
    const schema = cloneSchema(request.schema);
    const current = this.resources.get(schema.id) ?? makeResource(schema.id, {
      schema,
      name: schema.name,
      description: schema.description ?? ""
    });
    const next: MicroflowResource = {
      ...current,
      name: schema.name,
      description: schema.description ?? current.description,
      version: schema.audit.version,
      status: current.status === "published" ? "draft" : current.status,
      updatedAt: nowIso(),
      lastModifiedBy: "Admin",
      schema
    };
    this.resources.set(schema.id, next);
    this.persistResources();
    return {
      microflowId: schema.id,
      version: schema.audit.version,
      savedAt: next.updatedAt,
      nodeCount: flattenObjectCollection(schema.objectCollection).length,
      edgeCount: schema.flows.length
    };
  }

  async loadMicroflow(id: string): Promise<MicroflowAuthoringSchema> {
    return cloneSchema(this.resources.get(id)?.schema ?? sampleMicroflowSchema);
  }

  async validateMicroflow(request: ValidateMicroflowRequest): Promise<ValidateMicroflowResponse> {
    const { issues } = validateMicroflowSchema({ schema: request.schema, metadata: getDefaultMockMetadataCatalog() });
    return {
      valid: issues.every(item => item.severity !== "error"),
      issues,
    };
  }

  async testRunMicroflow(request: TestRunMicroflowRequest): Promise<TestRunMicroflowResponse> {
    const schema = request.schema ?? this.resources.get(request.microflowId ?? "")?.schema ?? sampleMicroflowSchema;
    const microflowId = request.microflowId ?? schema.id;
    const session = await mockTestRunMicroflow({
      schema,
      metadata: getDefaultMockMetadataCatalog(),
      variableIndex: buildVariableIndex(schema, getDefaultMockMetadataCatalog()),
      parameters: request.input,
      options: request.options,
    });
    this.traces.set(session.id, session.trace);
    this.sessions.set(session.id, session);
    this.runMicroflowIndex.set(session.id, microflowId);
    return {
      runId: session.id,
      status: session.status === "success" ? "succeeded" : session.status === "cancelled" ? "cancelled" : "failed",
      startedAt: session.startedAt,
      durationMs: session.trace.reduce((total, frame) => total + frame.durationMs, 0),
      frames: session.trace,
      error: session.error ?? session.trace.find(frame => frame.error)?.error,
      session,
    };
  }

  async cancelMicroflowRun(runId: string): Promise<{ runId: string; status: "cancelled" | "success" | "failed" }> {
    const session = this.sessions.get(runId);
    if (session) {
      this.sessions.set(runId, { ...session, status: "cancelled", endedAt: nowIso() });
    }
    return { runId, status: "cancelled" };
  }

  async getMicroflowRunSession(runId: string): Promise<MicroflowRunSession> {
    const session = this.sessions.get(runId);
    if (!session) {
      throw new Error(`Microflow run ${runId} was not found.`);
    }
    return session;
  }

  async listMicroflowRuns(microflowId: string, query: MicroflowRunHistoryQuery = {}): Promise<ListMicroflowRunsResponse> {
    const pageIndex = query.pageIndex ?? 1;
    const pageSize = query.pageSize ?? 20;
    const statusFilter = query.status ?? "all";
    const all = [...this.sessions.values()]
      .filter(session => this.runMicroflowIndex.get(session.id) === microflowId)
      .map(session => {
        const status: MicroflowRunHistoryStatus = session.status === "success" ? "success" : session.status === "cancelled" ? "cancelled" : "failed";
        const durationMs = session.endedAt
          ? Math.max(0, new Date(session.endedAt).getTime() - new Date(session.startedAt).getTime())
          : 0;
        return {
          runId: session.id,
          microflowId,
          status,
          durationMs,
          startedAt: session.startedAt,
          completedAt: session.endedAt,
          errorMessage: session.error?.message,
          summary: status === "success" ? "Run succeeded" : status === "cancelled" ? "Run cancelled" : "Run failed",
        };
      })
      .filter(item => statusFilter === "all" || item.status === statusFilter)
      .sort((a, b) => new Date(b.startedAt).getTime() - new Date(a.startedAt).getTime());
    const start = Math.max(0, (pageIndex - 1) * pageSize);
    return {
      items: all.slice(start, start + pageSize),
      total: all.length,
    };
  }

  async getMicroflowRunDetail(microflowId: string, runId: string): Promise<MicroflowRunSession> {
    if (this.runMicroflowIndex.get(runId) !== microflowId) {
      throw new Error(`Microflow run ${runId} does not belong to ${microflowId}.`);
    }
    return this.getMicroflowRunSession(runId);
  }

  async publishMicroflow(id: string, payload: PublishMicroflowPayload = { version: "v1", releaseNote: "", overwriteCurrent: true }): Promise<PublishMicroflowResponse> {
    const current = this.resources.get(id);
    if (!current) {
      throw new Error(`Microflow ${id} was not found.`);
    }
    const publishedAt = nowIso();
    const nextStatus: MicroflowResourceStatus = "published";
    const next: MicroflowResource = {
      ...current,
      status: nextStatus,
      version: payload.version.trim() || current.version,
      publishedAt,
      updatedAt: publishedAt,
      schema: {
        ...current.schema,
        audit: {
          ...current.schema.audit,
          version: payload.version.trim() || current.version
        }
      }
    };
    this.resources.set(id, next);
    this.persistResources();
    return {
      microflowId: next.id,
      publishedVersion: next.version,
      publishedAt,
      resource: cloneResource(next)
    };
  }

  async duplicateMicroflow(id: string): Promise<MicroflowResource> {
    const current = await this.getMicroflow(id);
    const nextId = `mf-${Date.now()}`;
    const schema = cloneSchema(current.schema);
    schema.id = nextId;
    schema.name = `${current.name} Copy`;
    const timestamp = nowIso();
    const duplicated: MicroflowResource = {
      ...current,
      id: nextId,
      name: `${current.name} Copy`,
      status: "draft",
      favorite: false,
      version: "v0.1",
      createdAt: timestamp,
      updatedAt: timestamp,
      publishedAt: undefined,
      schema
    };
    this.resources.set(nextId, duplicated);
    this.persistResources();
    return cloneResource(duplicated);
  }

  async deleteMicroflow(id: string): Promise<void> {
    this.resources.delete(id);
    this.persistResources();
  }

  async archiveMicroflow(id: string): Promise<MicroflowResource> {
    const current = await this.getMicroflow(id);
    const next = { ...current, status: "archived" as const, updatedAt: nowIso() };
    this.resources.set(id, next);
    this.persistResources();
    return cloneResource(next);
  }

  async toggleFavorite(id: string, favorite: boolean): Promise<MicroflowResource> {
    const current = await this.getMicroflow(id);
    const next = { ...current, favorite, updatedAt: nowIso() };
    this.resources.set(id, next);
    this.persistResources();
    return cloneResource(next);
  }

  async getMicroflowReferences(id: string): Promise<MicroflowReference[]> {
    const resource = this.resources.get(id);
    if (!resource || resource.status !== "published") {
      return [];
    }
    return [
      {
        id: `${id}-workflow-ref`,
        sourceType: "workflow",
        sourceName: "Order Approval Workflow",
        sourceId: "wf-order-approval",
        updatedAt: resource.updatedAt
      },
      {
        id: `${id}-lowcode-ref`,
        sourceType: "lowcode-app",
        sourceName: "Order Console",
        sourceId: "app-order-console",
        updatedAt: resource.updatedAt
      }
    ];
  }

  async getTrace(runId: string): Promise<MicroflowTraceFrame[]> {
    return [...(this.traces.get(runId) ?? [])];
  }

  async getMicroflowRunTrace(runId: string): Promise<MicroflowTraceFrame[]> {
    return this.getTrace(runId);
  }

  private compareResources(a: MicroflowResource, b: MicroflowResource, sortBy: MicroflowResourceSortKey): number {
    if (sortBy === "name") {
      return compareByString(a.name, b.name);
    }
    if (sortBy === "version") {
      return compareByString(b.version, a.version);
    }
    const left = new Date(a[sortBy]).getTime();
    const right = new Date(b[sortBy]).getTime();
    return (Number.isNaN(right) ? 0 : right) - (Number.isNaN(left) ? 0 : left);
  }

  private restoreResources(): MicroflowResource[] {
    if (typeof window === "undefined") {
      return [];
    }
    try {
      const raw = window.localStorage.getItem(STORAGE_KEY);
      if (!raw) {
        return [];
      }
      const parsed = JSON.parse(raw) as MicroflowResource[];
      return Array.isArray(parsed) ? parsed : [];
    } catch {
      return [];
    }
  }

  private persistResources(): void {
    if (typeof window === "undefined") {
      return;
    }
    try {
      window.localStorage.setItem(STORAGE_KEY, JSON.stringify([...this.resources.values()]));
    } catch {
      // localStorage may be disabled in private contexts; memory storage still keeps the UI usable.
    }
  }
}

export function createLocalMicroflowApiClient(initialSchemas?: MicroflowAuthoringSchema[]): LocalMicroflowApiClient {
  return new LocalMicroflowApiClient(initialSchemas);
}

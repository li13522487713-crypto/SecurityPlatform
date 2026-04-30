/**
 * Local development/offline debug only.
 * Do not use localStorage adapter as production persistence.
 */
import type {
  CreateMicroflowInput,
  MicroflowListQuery,
  MicroflowReference,
  MicroflowResource,
  MicroflowResourceSortKey,
  MicroflowResourceStatus,
  MicroflowDesignSchema,
  MicroflowValidationIssue,
  PublishMicroflowPayload
} from "../schema/types";
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

function cloneSchema(schema: MicroflowDesignSchema): MicroflowDesignSchema {
  return JSON.parse(JSON.stringify(schema)) as MicroflowDesignSchema;
}

function cloneResource(resource: MicroflowResource): MicroflowResource {
  return {
    ...resource,
    tags: [...resource.tags],
    schema: cloneSchema(resource.schema)
  };
}

function nowIso(): string {
  return new Date().toISOString();
}

function compareByString(a: string, b: string): number {
  return a.localeCompare(b, undefined, { numeric: true, sensitivity: "base" });
}

function validateDesignSchema(schema: MicroflowDesignSchema): MicroflowValidationIssue[] {
  const issues: MicroflowValidationIssue[] = [];
  const nodeIds = new Set<string>();
  schema.workflow.nodes.forEach((node, index) => {
    if (!node.id) {
      issues.push({
        id: `issue-node-id-${index}`,
        code: "MF_NODE_ID_MISSING",
        severity: "error",
        message: "节点 id 不能为空。",
        source: "node",
        fieldPath: `workflow.nodes.${index}.id`,
      });
      return;
    }
    if (nodeIds.has(node.id)) {
      issues.push({
        id: `issue-node-duplicate-${node.id}`,
        code: "MF_NODE_ID_DUPLICATED",
        severity: "error",
        message: `节点 id ${node.id} 重复。`,
        source: "node",
        nodeId: node.id,
        fieldPath: `workflow.nodes.${index}.id`,
      });
    }
    nodeIds.add(node.id);
  });
  schema.workflow.edges.forEach((edge, index) => {
    if (!nodeIds.has(edge.sourceNodeID) || !nodeIds.has(edge.targetNodeID)) {
      issues.push({
        id: `issue-edge-endpoint-${edge.id || index}`,
        code: "MF_EDGE_ENDPOINT_MISSING",
        severity: "error",
        message: "边端点必须引用 workflow.nodes 中存在的节点。",
        source: "flow",
        edgeId: edge.id,
        fieldPath: `workflow.edges.${index}`,
      });
    }
  });
  return issues;
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
  overrides: Partial<Omit<MicroflowResource, "id" | "schema">> & { schema: MicroflowDesignSchema }
): MicroflowResource {
  const timestamp = nowIso();
  const schema = cloneSchema(overrides.schema);
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

export class LocalMicroflowApiClient implements MicroflowApiClient {
  private readonly resources = new Map<string, MicroflowResource>();
  private readonly traces = new Map<string, MicroflowTraceFrame[]>();
  private readonly sessions = new Map<string, MicroflowRunSession>();
  private readonly runMicroflowIndex = new Map<string, string>();

  constructor(initialSchemas: MicroflowDesignSchema[] = []) {
    const restored = this.restoreResources();
    if (restored.length > 0) {
      for (const resource of restored) {
        this.resources.set(resource.id, cloneResource(resource));
      }
      return;
    }

    for (const schema of initialSchemas) {
      this.resources.set(schema.id, makeResource(schema.id, { schema, name: schema.name, description: schema.description ?? "" }));
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
    throw new Error(`LocalMicroflowApiClient.createMicroflow requires a real MicroflowDesignSchema from the resource adapter. Requested: ${input.name}`);
  }

  async getMicroflow(id: string): Promise<MicroflowResource> {
    const resource = this.resources.get(id);
    if (!resource) {
      throw new Error(`Microflow ${id} was not found.`);
    }
    return cloneResource(resource);
  }

  async saveMicroflow(request: SaveMicroflowRequest): Promise<SaveMicroflowResponse> {
    if (!("workflow" in request.schema)) {
      throw new Error("LocalMicroflowApiClient only saves MicroflowDesignSchema.");
    }
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
      nodeCount: schema.workflow.nodes.length,
      edgeCount: schema.workflow.edges.length
    };
  }

  async loadMicroflow(id: string): Promise<MicroflowDesignSchema> {
    const resource = this.resources.get(id);
    if (!resource) {
      throw new Error(`Microflow ${id} was not found.`);
    }
    return cloneSchema(resource.schema);
  }

  async validateMicroflow(request: ValidateMicroflowRequest): Promise<ValidateMicroflowResponse> {
    const issues = validateDesignSchema(request.schema);
    return {
      valid: issues.every(item => item.severity !== "error"),
      issues,
    };
  }

  async testRunMicroflow(request: TestRunMicroflowRequest): Promise<TestRunMicroflowResponse> {
    const selectedSchema = request.schema ?? this.resources.get(request.microflowId ?? "")?.schema;
    if (!selectedSchema) {
      throw new Error(`Microflow ${request.microflowId ?? "(unspecified)"} was not found.`);
    }
    if (!("workflow" in selectedSchema)) {
      throw new Error("LocalMicroflowApiClient only accepts MicroflowDesignSchema.");
    }
    const schema = cloneSchema(selectedSchema);
    const microflowId = request.microflowId ?? schema.id;
    const startedAt = nowIso();
    const session: MicroflowRunSession = {
      id: `run-${Date.now()}`,
      schemaId: schema.id,
      resourceId: microflowId,
      status: "unsupported",
      startedAt,
      endedAt: startedAt,
      input: request.input,
      output: {},
      trace: [],
      logs: [],
      variables: [],
      error: {
        code: "RUNTIME_UNSUPPORTED_ACTION",
        message: "本地离线适配器不再把新版设计态编译为旧 runtime schema。",
      },
    };
    this.traces.set(session.id, session.trace);
    this.sessions.set(session.id, session);
    this.runMicroflowIndex.set(session.id, microflowId);
    const errorCode = session.error?.code ?? session.trace.find(frame => frame.error)?.error?.code;
    return {
      runId: session.id,
      status: errorCode?.toUpperCase().includes("UNSUPPORTED")
        ? "unsupported"
        : session.status === "success"
          ? "succeeded"
          : session.status === "cancelled"
            ? "cancelled"
            : "failed",
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

export function createLocalMicroflowApiClient(initialSchemas?: MicroflowDesignSchema[]): LocalMicroflowApiClient {
  return new LocalMicroflowApiClient(initialSchemas);
}

import { validateMicroflowSchema } from "../schema/validator";
import type {
  CreateMicroflowInput,
  MicroflowListQuery,
  MicroflowReference,
  MicroflowResource,
  MicroflowResourceSortKey,
  MicroflowResourceStatus,
  MicroflowSchema,
  PublishMicroflowPayload
} from "../schema/types";
import { sampleMicroflowSchema } from "../schema/sample";
import type {
  MicroflowApiClient,
  MicroflowTraceFrame,
  PublishMicroflowResponse,
  SaveMicroflowRequest,
  SaveMicroflowResponse,
  TestRunMicroflowRequest,
  TestRunMicroflowResponse,
  ValidateMicroflowRequest,
  ValidateMicroflowResponse
} from "./types";

const STORAGE_KEY = "atlas_microflow_resources_v1";

function cloneSchema(schema: MicroflowSchema): MicroflowSchema {
  return JSON.parse(JSON.stringify(schema)) as MicroflowSchema;
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
  overrides: Partial<Omit<MicroflowResource, "id" | "schema">> & { schema?: MicroflowSchema }
): MicroflowResource {
  const timestamp = nowIso();
  const schema = cloneSchema(overrides.schema ?? sampleMicroflowSchema);
  schema.id = id;
  schema.name = overrides.name ?? schema.name;
  schema.description = overrides.description ?? schema.description;
  schema.version = overrides.version ?? schema.version;
  return {
    id,
    name: schema.name,
    description: schema.description ?? "",
    moduleId: "order",
    moduleName: "Order",
    ownerName: "Admin",
    sharedWithMe: false,
    tags: ["order", "demo"],
    version: schema.version,
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

  constructor(initialSchemas: MicroflowSchema[] = [sampleMicroflowSchema]) {
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
    schema.version = "v0.1";
    if (input.returnType?.kind && input.returnType.kind !== "void") {
      const endNode = schema.nodes.find(node => node.type === "endEvent");
      if (endNode?.type === "endEvent") {
        endNode.config.returnValue = {
          id: `${id}-return`,
          language: "mendix",
          text: "empty",
          expectedType: input.returnType
        };
      }
    }
    const resource = makeResource(id, {
      schema,
      name: input.name.trim(),
      description: input.description.trim(),
      moduleId: input.moduleId.trim() || "default",
      moduleName: input.moduleName?.trim() || input.moduleId.trim() || "Default",
      tags: input.tags,
      version: schema.version,
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
      version: schema.version,
      status: current.status === "published" ? "draft" : current.status,
      updatedAt: nowIso(),
      lastModifiedBy: "Admin",
      schema
    };
    this.resources.set(schema.id, next);
    this.persistResources();
    return {
      microflowId: schema.id,
      version: schema.version,
      savedAt: next.updatedAt,
      nodeCount: schema.nodes.length,
      edgeCount: schema.edges.length
    };
  }

  async loadMicroflow(id: string): Promise<MicroflowSchema> {
    return cloneSchema(this.resources.get(id)?.schema ?? sampleMicroflowSchema);
  }

  async validateMicroflow(request: ValidateMicroflowRequest): Promise<ValidateMicroflowResponse> {
    const issues = validateMicroflowSchema(request.schema);
    return {
      valid: issues.every(item => item.severity !== "error"),
      issues
    };
  }

  async testRunMicroflow(request: TestRunMicroflowRequest): Promise<TestRunMicroflowResponse> {
    const schema = request.schema ?? this.resources.get(request.microflowId)?.schema ?? sampleMicroflowSchema;
    const startedAt = Date.now();
    const runId = `run-${startedAt}`;
    const simulateError = String(request.input.simulateError ?? "").toLowerCase() === "true";
    const nodesById = new Map(schema.nodes.map(node => [node.id, node]));
    const orderedNodeIds: Array<{ nodeId: string; incomingEdgeId?: string; outgoingEdgeId?: string }> = [];
    const start = schema.nodes.find(node => node.type === "startEvent");
    let currentId = start?.id;
    let incomingEdgeId: string | undefined;
    const visited = new Set<string>();
    while (currentId && !visited.has(currentId)) {
      visited.add(currentId);
      const current = nodesById.get(currentId);
      if (!current || current.type === "annotation" || current.type === "parameter") {
        break;
      }
      const failedRest = simulateError && current.type === "activity" && current.config.activityType === "callRest";
      const outgoing = failedRest
        ? schema.edges.find(edge => edge.sourceNodeId === currentId && edge.type === "errorHandler")
        : schema.edges.find(edge => edge.sourceNodeId === currentId && edge.type !== "annotation" && edge.type !== "errorHandler" && (
          edge.type !== "decisionCondition" ||
          edge.conditionValue?.kind !== "boolean" ||
          edge.conditionValue.value === true
        ));
      orderedNodeIds.push({ nodeId: currentId, incomingEdgeId, outgoingEdgeId: outgoing?.id });
      incomingEdgeId = outgoing?.id;
      currentId = outgoing?.targetNodeId;
    }
    const traversableNodes: Array<{ nodeId: string; incomingEdgeId?: string; outgoingEdgeId?: string }> = orderedNodeIds.length > 0
      ? orderedNodeIds
      : schema.nodes.filter(node => node.type !== "annotation" && node.type !== "parameter").map(node => ({ nodeId: node.id }));
    const frames = traversableNodes.map((trace, index): MicroflowTraceFrame => {
      const node = nodesById.get(trace.nodeId) ?? schema.nodes[index];
      const durationMs = 8 + index * 3;
      const failed = simulateError && node?.type === "activity" && node.config.activityType === "callRest";
      return {
        id: `${runId}-${trace.nodeId}`,
        frameId: `${runId}-${node.id}`,
        runId,
        nodeId: node.id,
        nodeTitle: node.title,
        incomingEdgeId: trace.incomingEdgeId,
        outgoingEdgeId: trace.outgoingEdgeId,
        status: failed ? "failed" : "success",
        startedAt: new Date(startedAt + index * 12).toISOString(),
        durationMs,
        input: index === 0 ? request.input : { previousNodeId: traversableNodes[index - 1]?.nodeId, incomingEdgeId: trace.incomingEdgeId },
        output: {
          status: failed ? "failed" : "ok",
          nodeType: node.type,
          activityType: node.type === "activity" ? node.config.activityType : undefined,
          outgoingEdgeId: trace.outgoingEdgeId,
          conditionValue: schema.edges.find(edge => edge.id === trace.outgoingEdgeId)?.conditionValue
        },
        error: failed ? {
          code: "MF_TEST_REST_ERROR",
          message: "Mock REST call failed. Set simulateError=false to run the success path.",
          nodeId: node.id,
          details: { url: node.config.url }
        } : undefined
      };
    });
    const failedFrame = frames.find(frame => frame.error);
    this.traces.set(runId, frames);
    return {
      runId,
      status: failedFrame ? "failed" : "succeeded",
      startedAt: new Date(startedAt).toISOString(),
      durationMs: frames.reduce((total, frame) => total + frame.durationMs, 0),
      frames,
      error: failedFrame?.error
    };
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
        version: payload.version.trim() || current.schema.version
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

export function createLocalMicroflowApiClient(initialSchemas?: MicroflowSchema[]): LocalMicroflowApiClient {
  return new LocalMicroflowApiClient(initialSchemas);
}

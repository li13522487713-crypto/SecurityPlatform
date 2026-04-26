import { validateMicroflowSchema } from "../schema/validator";
import type { MicroflowSchema } from "../schema/types";
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

function cloneSchema(schema: MicroflowSchema): MicroflowSchema {
  return JSON.parse(JSON.stringify(schema)) as MicroflowSchema;
}

function nowIso(): string {
  return new Date().toISOString();
}

export class LocalMicroflowApiClient implements MicroflowApiClient {
  private readonly schemas = new Map<string, MicroflowSchema>();
  private readonly traces = new Map<string, MicroflowTraceFrame[]>();

  constructor(initialSchemas: MicroflowSchema[] = [sampleMicroflowSchema]) {
    for (const schema of initialSchemas) {
      this.schemas.set(schema.id, cloneSchema(schema));
    }
  }

  async saveMicroflow(request: SaveMicroflowRequest): Promise<SaveMicroflowResponse> {
    const schema = cloneSchema(request.schema);
    this.schemas.set(schema.id, schema);
    return {
      microflowId: schema.id,
      version: schema.version,
      savedAt: nowIso(),
      nodeCount: schema.nodes.length,
      edgeCount: schema.edges.length
    };
  }

  async loadMicroflow(id: string): Promise<MicroflowSchema> {
    return cloneSchema(this.schemas.get(id) ?? sampleMicroflowSchema);
  }

  async validateMicroflow(request: ValidateMicroflowRequest): Promise<ValidateMicroflowResponse> {
    const issues = validateMicroflowSchema(request.schema);
    return {
      valid: issues.every(item => item.severity !== "error"),
      issues
    };
  }

  async testRunMicroflow(request: TestRunMicroflowRequest): Promise<TestRunMicroflowResponse> {
    const schema = request.schema ?? this.schemas.get(request.microflowId) ?? sampleMicroflowSchema;
    const startedAt = Date.now();
    const runId = `run-${startedAt}`;
    const traversableNodes = schema.nodes.filter(node => node.type !== "annotation" && node.type !== "parameter");
    const frames = traversableNodes.map((node, index): MicroflowTraceFrame => {
      const durationMs = 8 + index * 3;
      return {
        frameId: `${runId}-${node.id}`,
        runId,
        nodeId: node.id,
        nodeTitle: node.title,
        startedAt: new Date(startedAt + index * 12).toISOString(),
        durationMs,
        input: index === 0 ? request.input : { previousNodeId: traversableNodes[index - 1]?.id },
        output: {
          status: "ok",
          nodeType: node.type,
          activityType: node.type === "activity" ? node.config.activityType : undefined
        }
      };
    });
    this.traces.set(runId, frames);
    return {
      runId,
      status: "succeeded",
      startedAt: new Date(startedAt).toISOString(),
      durationMs: frames.reduce((total, frame) => total + frame.durationMs, 0),
      frames
    };
  }

  async publishMicroflow(id: string): Promise<PublishMicroflowResponse> {
    const schema = this.schemas.get(id) ?? sampleMicroflowSchema;
    return {
      microflowId: schema.id,
      publishedVersion: schema.version,
      publishedAt: nowIso()
    };
  }

  async getTrace(runId: string): Promise<MicroflowTraceFrame[]> {
    return [...(this.traces.get(runId) ?? [])];
  }
}

export function createLocalMicroflowApiClient(initialSchemas?: MicroflowSchema[]): LocalMicroflowApiClient {
  return new LocalMicroflowApiClient(initialSchemas);
}

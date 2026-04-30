import type { MicroflowDesignSchema, MicroflowValidationIssue } from "@atlas/microflow";
import type { MicroflowMetadataCatalog } from "@atlas/microflow/metadata";

import { MicroflowApiClient, type MicroflowApiClientOptions } from "./http/microflow-api-client";

function asServerValidationIssue(issue: MicroflowValidationIssue, microflowId: string): MicroflowValidationIssue {
  return {
    ...issue,
    id: issue.id.startsWith(`${microflowId}:server:`) ? issue.id : `${microflowId}:server:${issue.id}`,
    microflowId,
    source: issue.source ?? "server",
    blockSave: issue.blockSave ?? issue.severity === "error",
    blockPublish: issue.blockPublish ?? issue.severity === "error",
  };
}

export type MicroflowValidationMode = "edit" | "save" | "publish" | "testRun";

export interface MicroflowValidationInput {
  resourceId?: string;
  schema: MicroflowDesignSchema;
  metadata?: MicroflowMetadataCatalog;
  mode: MicroflowValidationMode;
  includeInfo?: boolean;
  includeWarnings?: boolean;
}

export interface MicroflowValidationResult {
  issues: MicroflowValidationIssue[];
  summary: {
    errorCount: number;
    warningCount: number;
    infoCount: number;
  };
  serverValidatedAt?: string;
}

export interface MicroflowValidationAdapter {
  validate(input: MicroflowValidationInput): Promise<MicroflowValidationResult>;
}

function issue(id: string, code: string, message: string, fieldPath: string, microflowId: string): MicroflowValidationIssue {
  return {
    id: `${microflowId}:local:${id}`,
    code,
    severity: "error",
    message,
    source: "schema",
    fieldPath,
    microflowId,
    blockSave: true,
    blockPublish: true,
  };
}

function validateDesignSchemaLocally(schema: MicroflowDesignSchema): MicroflowValidationIssue[] {
  const issues: MicroflowValidationIssue[] = [];
  if (schema.schemaVersion !== "flowgram.microflow.v1") {
    issues.push(issue("schema-version", "MICROFLOW_SCHEMA_INVALID", "当前微流不是新版设计态。", "schemaVersion", schema.id));
  }
  if (!schema.workflow || !Array.isArray(schema.workflow.nodes) || !Array.isArray(schema.workflow.edges)) {
    issues.push(issue("workflow", "MICROFLOW_SCHEMA_INVALID", "workflow.nodes/workflow.edges 不能为空。", "workflow", schema.id));
    return issues;
  }
  const nodeIds = new Set<string>();
  for (const [index, node] of schema.workflow.nodes.entries()) {
    if (!node.id) {
      issues.push(issue(`node-${index}-id`, "MF_OBJECT_MISSING", "节点 id 不能为空。", `workflow.nodes.${index}.id`, schema.id));
      continue;
    }
    if (nodeIds.has(node.id)) {
      issues.push(issue(`node-${node.id}-duplicated`, "MF_OBJECT_ID_DUPLICATED", `节点 ${node.id} 重复。`, `workflow.nodes.${index}.id`, schema.id));
    }
    nodeIds.add(node.id);
  }
  for (const [index, edge] of schema.workflow.edges.entries()) {
    if (!nodeIds.has(edge.sourceNodeID)) {
      issues.push(issue(`edge-${index}-source`, "MF_FLOW_INVALID_SOURCE", "连线起点节点不存在。", `workflow.edges.${index}.sourceNodeID`, schema.id));
    }
    if (!nodeIds.has(edge.targetNodeID)) {
      issues.push(issue(`edge-${index}-target`, "MF_FLOW_INVALID_TARGET", "连线终点节点不存在。", `workflow.edges.${index}.targetNodeID`, schema.id));
    }
  }
  return issues;
}

export function createLocalMicroflowValidationAdapter(): MicroflowValidationAdapter {
  return {
    async validate(input) {
      const issues = validateDesignSchemaLocally(input.schema);
      return {
        issues,
        summary: {
          errorCount: issues.filter(item => item.severity === "error").length,
          warningCount: issues.filter(item => item.severity === "warning").length,
          infoCount: issues.filter(item => item.severity === "info").length,
        },
      };
    },
  };
}

export interface HttpMicroflowValidationAdapterOptions extends MicroflowApiClientOptions {
  apiClient?: MicroflowApiClient;
}

export function createHttpMicroflowValidationAdapter(options: HttpMicroflowValidationAdapterOptions): MicroflowValidationAdapter {
  const client = options.apiClient ?? new MicroflowApiClient(options);
  return {
    async validate(input) {
      const id = input.resourceId ?? input.schema.id;
      const result = await client.post<MicroflowValidationResult>(`/microflows/${encodeURIComponent(id)}/validate`, {
        schema: input.schema,
        mode: input.mode,
        includeWarnings: input.includeWarnings ?? true,
        includeInfo: input.includeInfo ?? true,
      });
      const issues = result.issues.map(issue => asServerValidationIssue(issue, id));
      return {
        ...result,
        issues,
        summary: {
          errorCount: issues.filter(issue => issue.severity === "error").length,
          warningCount: issues.filter(issue => issue.severity === "warning").length,
          infoCount: issues.filter(issue => issue.severity === "info").length,
        },
      };
    },
  };
}

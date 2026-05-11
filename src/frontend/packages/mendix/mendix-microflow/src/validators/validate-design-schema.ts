import { emptyVariableIndex } from "../adapters";
import type { MicroflowMetadataCatalog } from "../metadata";
import { buildDesignPropertyPanelModel } from "../property-panel/design-protocol-model";
import type {
  MicroflowDesignSchema,
  MicroflowValidationIssue,
  MicroflowVariableIndex,
  MicroflowWorkflowEdgeJSON,
  MicroflowWorkflowNodeJSON,
} from "../schema/types";
import {
  collectMicroflowBestPracticeWarnings,
  MICROFLOW_LIMITS,
  summarizeMicroflowComplexity,
  validateVariableNames,
} from "../utils/microflow-validator";
import { issue } from "./shared";
import { validateVariables } from "./validate-variables";
import type { MicroflowValidationOptions, MicroflowValidationResult } from "./validator-types";

function summarizeIssues(issues: MicroflowValidationIssue[]): MicroflowValidationResult["summary"] {
  return {
    errorCount: issues.filter(item => item.severity === "error").length,
    warningCount: issues.filter(item => item.severity === "warning").length,
    infoCount: issues.filter(item => item.severity === "info").length,
  };
}

function filterIssues(issues: MicroflowValidationIssue[], options?: MicroflowValidationOptions): MicroflowValidationIssue[] {
  const includeWarnings = options?.includeWarnings !== false;
  const includeInfo = options?.includeInfo === true;
  return issues.filter(item =>
    item.severity === "error" ||
    (item.severity === "warning" && includeWarnings) ||
    (item.severity === "info" && includeInfo),
  );
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return value != null && typeof value === "object";
}

export function isMicroflowDesignSchema(value: unknown): value is MicroflowDesignSchema {
  return isRecord(value) && isRecord(value.workflow);
}

function schemaIssue(schema: Partial<MicroflowDesignSchema>, code: string, message: string, fieldPath: string): MicroflowValidationIssue {
  return issue(
    code,
    message,
    {
      microflowId: schema.id,
      source: "schema",
      fieldPath,
      blockSave: true,
      blockPublish: true,
    },
    "error",
  );
}

function validateWorkflowNode(
  schema: MicroflowDesignSchema,
  node: MicroflowWorkflowNodeJSON,
  index: number,
  seenNodeIds: Set<string>,
  issues: MicroflowValidationIssue[],
): void {
  if (!node.id) {
    issues.push(issue(
      "MF_OBJECT_MISSING",
      "Microflow workflow node must have id.",
      {
        microflowId: schema.id,
        source: "node",
        fieldPath: `workflow.nodes.${index}.id`,
        nodeId: node.id,
        objectId: node.id,
      },
      "error",
    ));
    return;
  }
  if (seenNodeIds.has(node.id)) {
    issues.push(issue(
      "MF_OBJECT_ID_DUPLICATED",
      `Microflow workflow node id '${node.id}' is duplicated.`,
      {
        microflowId: schema.id,
        source: "node",
        fieldPath: `workflow.nodes.${index}.id`,
        nodeId: node.id,
        objectId: node.id,
      },
      "error",
    ));
    return;
  }
  seenNodeIds.add(node.id);
}

function validateWorkflowEdge(
  schema: MicroflowDesignSchema,
  edge: MicroflowWorkflowEdgeJSON,
  index: number,
  nodeIds: Set<string>,
  seenEdgeIds: Set<string>,
  issues: MicroflowValidationIssue[],
): void {
  const edgeId = String(edge.data?.flowId ?? edge.id ?? `workflow.edges.${index}`);
  if (edge.id && seenEdgeIds.has(edge.id)) {
    issues.push(issue(
      "MF_FLOW_ID_DUPLICATED",
      `Microflow workflow edge id '${edge.id}' is duplicated.`,
      {
        microflowId: schema.id,
        source: "flow",
        fieldPath: `workflow.edges.${index}.id`,
        edgeId: edge.id,
        flowId: edgeId,
      },
      "error",
    ));
  }
  if (edge.id) {
    seenEdgeIds.add(edge.id);
  }
  if (!edge.sourceNodeID) {
    issues.push(issue(
      "MF_FLOW_ORIGIN_MISSING",
      "Microflow workflow edge must have sourceNodeID.",
      {
        microflowId: schema.id,
        source: "flow",
        fieldPath: `workflow.edges.${index}.sourceNodeID`,
        edgeId,
        flowId: edgeId,
      },
      "error",
    ));
  } else if (!nodeIds.has(edge.sourceNodeID)) {
    issues.push(issue(
      "MF_FLOW_INVALID_SOURCE",
      "Microflow workflow edge source node does not exist.",
      {
        microflowId: schema.id,
        source: "flow",
        fieldPath: `workflow.edges.${index}.sourceNodeID`,
        edgeId,
        flowId: edgeId,
        objectId: edge.sourceNodeID,
      },
      "error",
    ));
  }
  if (!edge.targetNodeID) {
    issues.push(issue(
      "MF_FLOW_DESTINATION_MISSING",
      "Microflow workflow edge must have targetNodeID.",
      {
        microflowId: schema.id,
        source: "flow",
        fieldPath: `workflow.edges.${index}.targetNodeID`,
        edgeId,
        flowId: edgeId,
      },
      "error",
    ));
  } else if (!nodeIds.has(edge.targetNodeID)) {
    issues.push(issue(
      "MF_FLOW_INVALID_TARGET",
      "Microflow workflow edge target node does not exist.",
      {
        microflowId: schema.id,
        source: "flow",
        fieldPath: `workflow.edges.${index}.targetNodeID`,
        edgeId,
        flowId: edgeId,
        objectId: edge.targetNodeID,
      },
      "error",
    ));
  }
}

function validateDesignSchemaShape(schema: MicroflowDesignSchema): MicroflowValidationIssue[] {
  const issues: MicroflowValidationIssue[] = [];
  if (schema.schemaVersion !== "flowgram.microflow.v1") {
    issues.push(schemaIssue(schema, "MF_ROOT_SCHEMA_INVALID", "Microflow design schemaVersion must be flowgram.microflow.v1.", "schemaVersion"));
  }
  if (!Array.isArray(schema.parameters)) {
    issues.push(schemaIssue(schema, "MF_PARAMETERS_MISSING", "Microflow design schema must have parameters array.", "parameters"));
  }
  if (!isRecord(schema.workflow)) {
    issues.push(schemaIssue(schema, "MF_ROOT_SCHEMA_INVALID", "Microflow design schema must have workflow.", "workflow"));
    return issues;
  }
  if (!Array.isArray(schema.workflow.nodes)) {
    issues.push(schemaIssue(schema, "MF_OBJECT_COLLECTION_MISSING", "Microflow design schema must have workflow.nodes array.", "workflow.nodes"));
  }
  if (!Array.isArray(schema.workflow.edges)) {
    issues.push(schemaIssue(schema, "MF_FLOWS_MISSING", "Microflow design schema must have workflow.edges array.", "workflow.edges"));
  }
  if (issues.some(item => item.severity === "error")) {
    return issues;
  }

  const nodeIds = new Set<string>();
  for (const [index, node] of schema.workflow.nodes.entries()) {
    validateWorkflowNode(schema, node, index, nodeIds, issues);
  }
  const edgeIds = new Set<string>();
  for (const [index, edge] of schema.workflow.edges.entries()) {
    validateWorkflowEdge(schema, edge, index, nodeIds, edgeIds, issues);
  }
  return issues;
}

function validateDesignSchemaComplexity(schema: MicroflowDesignSchema): MicroflowValidationIssue[] {
  const issues: MicroflowValidationIssue[] = [];
  const summary = summarizeMicroflowComplexity(schema);

  if (summary.totalElements >= MICROFLOW_LIMITS.ERROR_LEVEL) {
    issues.push(issue(
      "MF_TOO_LARGE",
      `微流包含 ${summary.totalElements} 个元素，超过推荐上限 25 个。建议将部分逻辑提取为子微流。`,
      { microflowId: schema.id, source: "schema", fieldPath: "workflow.nodes", blockSave: false, blockPublish: false },
      "warning",
    ));
  } else if (summary.totalElements >= MICROFLOW_LIMITS.WARN_LEVEL) {
    issues.push(issue(
      "MF_APPROACHING_LIMIT",
      `微流包含 ${summary.totalElements} 个元素，接近推荐上限 25 个。`,
      { microflowId: schema.id, source: "schema", fieldPath: "workflow.nodes", blockSave: false, blockPublish: false },
      "warning",
    ));
  }

  if (summary.annotationRecommended && !summary.hasAnnotation) {
    issues.push(issue(
      "MF_MISSING_ANNOTATION",
      "复杂微流（超过 10 个活动或 2 个 Decision）建议在起始处添加注释说明目的和参数。",
      { microflowId: schema.id, source: "schema", fieldPath: "workflow.nodes", blockSave: false, blockPublish: false },
      "warning",
    ));
  }

  return issues;
}

function validateDesignSchemaBestPractices(schema: MicroflowDesignSchema): MicroflowValidationIssue[] {
  return collectMicroflowBestPracticeWarnings(schema).map(warning => issue(
    warning.code,
    warning.message,
    {
      microflowId: schema.id,
      source: "action",
      objectId: warning.objectId,
      fieldPath: warning.fieldPath,
      blockSave: false,
      blockPublish: false,
    },
    warning.severity,
  ));
}

function validateDesignSchemaVariableNames(schema: MicroflowDesignSchema): MicroflowValidationIssue[] {
  return validateVariableNames(schema).map(conflict => issue(
    conflict.code,
    conflict.message,
    {
      microflowId: schema.id,
      source: "action",
      objectId: conflict.nodeIds[0],
      relatedObjectIds: conflict.nodeIds.slice(1),
      fieldPath: "workflow.nodes",
      blockSave: true,
      blockPublish: true,
    },
    "error",
  ));
}

export function validateMicroflowDesignSchema(input: {
  schema: MicroflowDesignSchema;
  metadata?: MicroflowMetadataCatalog | null;
  variableIndex?: MicroflowVariableIndex;
  options?: MicroflowValidationOptions;
}): MicroflowValidationResult {
  const authoringModel = input.metadata ? buildDesignPropertyPanelModel(input.schema) : undefined;
  const variableIndex = input.variableIndex
    ?? (authoringModel && input.metadata ? authoringModel.authoringSchema.variables ?? emptyVariableIndex() : emptyVariableIndex());
  const variableIssues = authoringModel && input.metadata
    ? validateVariables(authoringModel.authoringSchema, {
      metadata: input.metadata,
      variableIndex,
      mode: input.options?.mode ?? "edit",
    })
    : [];
  const issues = filterIssues([
    ...validateDesignSchemaShape(input.schema),
    ...validateDesignSchemaComplexity(input.schema),
    ...validateDesignSchemaBestPractices(input.schema),
    ...validateDesignSchemaVariableNames(input.schema),
    ...variableIssues,
  ], input.options);
  return {
    issues,
    variableIndex,
    summary: summarizeIssues(issues),
  };
}


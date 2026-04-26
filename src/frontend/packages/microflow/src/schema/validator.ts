import type {
  MicroflowActivityNode,
  MicroflowEdge,
  MicroflowNode,
  MicroflowSchema,
  MicroflowValidationIssue
} from "./types";

function issue(
  code: string,
  message: string,
  target?: Pick<MicroflowValidationIssue, "nodeId" | "edgeId">,
  severity: MicroflowValidationIssue["severity"] = "error"
): MicroflowValidationIssue {
  return {
    id: `${code}:${target?.nodeId ?? target?.edgeId ?? "schema"}`,
    code,
    message,
    severity,
    ...target
  };
}

function isActivityNode(node: MicroflowNode): node is MicroflowActivityNode {
  return node.type === "activity";
}

function hasText(value: string | undefined): boolean {
  return typeof value === "string" && value.trim().length > 0;
}

function getIncoming(edges: MicroflowEdge[], nodeId: string): MicroflowEdge[] {
  return edges.filter(edge => edge.targetNodeId === nodeId);
}

function getOutgoing(edges: MicroflowEdge[], nodeId: string): MicroflowEdge[] {
  return edges.filter(edge => edge.sourceNodeId === nodeId);
}

export function validateMicroflowSchema(schema: MicroflowSchema): MicroflowValidationIssue[] {
  const issues: MicroflowValidationIssue[] = [];
  const nodesById = new Map(schema.nodes.map(node => [node.id, node]));
  const startEvents = schema.nodes.filter(node => node.type === "startEvent");
  const endEvents = schema.nodes.filter(node => node.type === "endEvent");

  if (startEvents.length !== 1) {
    issues.push(issue("MF_START_EVENT_COUNT", "Microflow must contain exactly one Start Event."));
  }

  if (endEvents.length < 1) {
    issues.push(issue("MF_END_EVENT_REQUIRED", "Microflow must contain at least one End Event."));
  }

  for (const start of startEvents) {
    if (getIncoming(schema.edges, start.id).length > 0) {
      issues.push(issue("MF_START_HAS_INCOMING", "Start Event cannot have incoming flows.", { nodeId: start.id }));
    }
  }

  for (const end of endEvents) {
    if (getOutgoing(schema.edges, end.id).length > 0) {
      issues.push(issue("MF_END_HAS_OUTGOING", "End Event cannot have outgoing flows.", { nodeId: end.id }));
    }
  }

  const variableNames = new Set<string>();
  for (const variable of schema.variables) {
    const normalizedName = variable.name.trim();
    if (variableNames.has(normalizedName)) {
      issues.push(issue("MF_DUPLICATE_VARIABLE", `Variable "${variable.name}" is duplicated.`));
    }
    variableNames.add(normalizedName);
  }

  for (const edge of schema.edges) {
    const source = nodesById.get(edge.sourceNodeId);
    const target = nodesById.get(edge.targetNodeId);
    if (!source) {
      issues.push(issue("MF_EDGE_SOURCE_MISSING", "Flow source node does not exist.", { edgeId: edge.id }));
    }
    if (!target) {
      issues.push(issue("MF_EDGE_TARGET_MISSING", "Flow target node does not exist.", { edgeId: edge.id }));
    }
    if (edge.type === "error") {
      if (!source || !isActivityNode(source) || source.config.supportsErrorFlow !== true) {
        issues.push(issue("MF_ERROR_FLOW_SOURCE", "Error Flow can only start from an Activity that supports error handling.", { edgeId: edge.id }));
      }
    }
  }

  for (const node of schema.nodes) {
    if (node.type === "decision" && getOutgoing(schema.edges, node.id).length < 2) {
      issues.push(issue("MF_DECISION_OUTGOING", "Decision must have at least two outgoing flows.", { nodeId: node.id }));
    }

    if (node.type === "merge" && getIncoming(schema.edges, node.id).length < 2) {
      issues.push(issue("MF_MERGE_INCOMING", "Merge must have at least two incoming flows.", { nodeId: node.id }));
    }

    if (node.type === "loop" && !hasText(node.config.iterableVariableName)) {
      issues.push(issue("MF_LOOP_ITERABLE", "Loop must configure an iterable/list variable.", { nodeId: node.id }));
    }

    if (node.type !== "activity") {
      continue;
    }

    const config = node.config;
    if (!config.activityType) {
      issues.push(issue("MF_ACTIVITY_TYPE", "Activity must configure activityType.", { nodeId: node.id }));
      continue;
    }

    if (config.activityType === "callMicroflow" && !hasText(config.targetMicroflowId)) {
      issues.push(issue("MF_CALL_MICROFLOW_TARGET", "Call Microflow must configure targetMicroflowId.", { nodeId: node.id }));
    }

    if (config.activityType === "callRest" && (!config.method || !hasText(config.url))) {
      issues.push(issue("MF_CALL_REST_REQUEST", "Call REST must configure method and url.", { nodeId: node.id }));
    }

    if (config.activityType === "objectRetrieve" && !hasText(config.entity) && !hasText(config.association)) {
      issues.push(issue("MF_OBJECT_RETRIEVE_SOURCE", "Object Retrieve must configure entity or association.", { nodeId: node.id }));
    }

    if (
      (config.activityType === "objectCommit" ||
        config.activityType === "objectDelete" ||
        config.activityType === "objectRollback") &&
      !hasText(config.objectVariableName)
    ) {
      issues.push(issue("MF_OBJECT_VARIABLE_REQUIRED", "Object Commit/Delete/Rollback must configure object variable.", { nodeId: node.id }));
    }
  }

  return issues;
}

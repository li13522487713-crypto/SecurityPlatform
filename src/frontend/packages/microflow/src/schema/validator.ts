import type {
  MicroflowActivityNode,
  MicroflowEdge,
  MicroflowNode,
  MicroflowNodeKind,
  MicroflowSchema,
  MicroflowValidationIssue
} from "./types";
import { defaultMicroflowEdgeRegistry } from "../node-registry/edge-registry";

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

function nodeKind(node: MicroflowNode): MicroflowNodeKind {
  if (node.kind) {
    return node.kind;
  }
  if (["startEvent", "endEvent", "errorEvent", "breakEvent", "continueEvent"].includes(node.type)) {
    return "event";
  }
  return node.type as MicroflowNodeKind;
}

function isExecutable(node: MicroflowNode): boolean {
  return node.type !== "parameter" && node.type !== "annotation";
}

function isTerminal(node: MicroflowNode): boolean {
  return ["endEvent", "errorEvent", "breakEvent", "continueEvent"].includes(node.type);
}

function isInErrorScope(schema: MicroflowSchema, nodeId: string): boolean {
  const errorSources = new Set(schema.edges.filter(edge => edge.type === "errorHandler").map(edge => edge.targetNodeId));
  const queue = [...errorSources];
  const visited = new Set<string>();
  while (queue.length > 0) {
    const current = queue.shift();
    if (!current || visited.has(current)) {
      continue;
    }
    visited.add(current);
    if (current === nodeId) {
      return true;
    }
    for (const edge of schema.edges.filter(item => item.sourceNodeId === current && item.type !== "annotation")) {
      queue.push(edge.targetNodeId);
    }
  }
  return false;
}

function reachableFromStart(schema: MicroflowSchema, startId: string): Set<string> {
  const visited = new Set<string>();
  const queue = [startId];
  while (queue.length > 0) {
    const current = queue.shift();
    if (!current || visited.has(current)) {
      continue;
    }
    visited.add(current);
    for (const edge of schema.edges.filter(item => item.sourceNodeId === current && item.type !== "annotation")) {
      queue.push(edge.targetNodeId);
    }
  }
  return visited;
}

function canReachTerminal(schema: MicroflowSchema, nodeId: string): boolean {
  const visited = new Set<string>();
  const queue = [nodeId];
  while (queue.length > 0) {
    const current = queue.shift();
    if (!current || visited.has(current)) {
      continue;
    }
    visited.add(current);
    const node = schema.nodes.find(item => item.id === current);
    if (node && (node.type === "endEvent" || node.type === "errorEvent")) {
      return true;
    }
    for (const edge of schema.edges.filter(item => item.sourceNodeId === current && item.type !== "annotation")) {
      queue.push(edge.targetNodeId);
    }
  }
  return false;
}

function conditionKey(edge: MicroflowEdge): string {
  const value = edge.conditionValue;
  if (!value) {
    return "";
  }
  if (value.kind === "objectType") {
    return `objectType:${value.entity}`;
  }
  return `${value.kind}:${value.value}`;
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
    if (getOutgoing(schema.edges, start.id).filter(edge => edge.type === "sequence").length !== 1) {
      issues.push(issue("MF_START_OUTGOING", "Start Event must have exactly one normal Sequence Flow.", { nodeId: start.id }));
    }
    if (start.parentLoopId) {
      issues.push(issue("MF_START_IN_LOOP", "Start Event cannot be placed inside a Loop.", { nodeId: start.id }));
    }
  }

  const returnTypes = new Set<string>();
  for (const end of endEvents) {
    if (getOutgoing(schema.edges, end.id).length > 0) {
      issues.push(issue("MF_END_HAS_OUTGOING", "End Event cannot have outgoing flows.", { nodeId: end.id }));
    }
    const returnTypeName = end.config.returnType?.name ?? (end.config.returnValue ? end.config.returnValue.expectedType?.name : "Void") ?? "Void";
    returnTypes.add(returnTypeName);
    if (returnTypeName !== "Void" && !hasText(end.config.returnValue?.text)) {
      issues.push(issue("MF_END_RETURN_VALUE", "End Event with non-Void return type must configure return value expression.", { nodeId: end.id }));
    }
    if (end.parentLoopId) {
      issues.push(issue("MF_END_IN_LOOP", "End Event cannot be placed inside a Loop.", { nodeId: end.id }));
    }
  }
  if (returnTypes.size > 1) {
    issues.push(issue("MF_END_RETURN_TYPE_MISMATCH", "All End Events must use the same return type."));
  }

  const variableNames = new Set<string>();
  const parameterNames = new Set<string>();
  for (const parameter of schema.parameters) {
    const normalizedName = parameter.name.trim();
    if (!normalizedName) {
      issues.push(issue("MF_PARAMETER_NAME_REQUIRED", "Parameter name is required."));
    }
    if (parameterNames.has(normalizedName)) {
      issues.push(issue("MF_DUPLICATE_PARAMETER", `Parameter "${parameter.name}" is duplicated.`));
    }
    parameterNames.add(normalizedName);
    if (!parameter.type?.name) {
      issues.push(issue("MF_PARAMETER_TYPE_REQUIRED", `Parameter "${parameter.name}" must configure a valid type.`));
    }
  }
  for (const variable of schema.variables) {
    const normalizedName = variable.name.trim();
    if (variableNames.has(normalizedName)) {
      issues.push(issue("MF_DUPLICATE_VARIABLE", `Variable "${variable.name}" is duplicated.`));
    }
    if (parameterNames.has(normalizedName)) {
      issues.push(issue("MF_DUPLICATE_PARAMETER_VARIABLE", `Variable "${variable.name}" duplicates a parameter name.`));
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
    const registryItem = defaultMicroflowEdgeRegistry.find(item => item.kind === edge.type);
    if (registryItem) {
      issues.push(...registryItem.validate(edge, { nodes: schema.nodes, edges: schema.edges }));
    }
    if (source && target) {
      if (edge.type === "sequence") {
        if (["endEvent", "errorEvent", "breakEvent", "continueEvent", "parameter", "annotation"].includes(source.type)) {
          issues.push(issue("MF_SEQUENCE_SOURCE_FORBIDDEN", "Sequence Flow cannot start from End/Error/Break/Continue/Parameter/Annotation.", { edgeId: edge.id }));
        }
        if (["startEvent", "parameter", "annotation"].includes(target.type)) {
          issues.push(issue("MF_SEQUENCE_TARGET_FORBIDDEN", "Sequence Flow cannot target Start/Parameter/Annotation.", { edgeId: edge.id }));
        }
        if (target.type === "errorEvent" && !isInErrorScope(schema, source.id)) {
          issues.push(issue("MF_SEQUENCE_TO_ERROR_EVENT", "Normal Sequence Flow cannot directly enter Error Event outside error scope.", { edgeId: edge.id }));
        }
      }
      if (edge.type === "annotation" && source.type !== "annotation" && target.type !== "annotation") {
        issues.push(issue("MF_ANNOTATION_EDGE_ENDPOINT", "Annotation Flow must connect to at least one Annotation.", { edgeId: edge.id }));
      }
      if ((source.type === "parameter" || target.type === "parameter" || source.type === "annotation" || target.type === "annotation") && edge.type !== "annotation") {
        issues.push(issue("MF_NON_EXECUTABLE_SEQUENCE", "Parameter and Annotation nodes can only use Annotation Flow.", { edgeId: edge.id }));
      }
    }
  }

  const connectionKeys = new Set<string>();
  for (const edge of schema.edges) {
    const key = `${edge.sourceNodeId}:${edge.sourcePortId ?? ""}->${edge.targetNodeId}:${edge.targetPortId ?? ""}:${edge.type}`;
    if (connectionKeys.has(key)) {
      issues.push(issue("MF_DUPLICATE_EDGE", "Duplicate connection from the same source port to the same target port.", { edgeId: edge.id }));
    }
    connectionKeys.add(key);
  }

  for (const node of schema.nodes) {
    const incoming = getIncoming(schema.edges, node.id).filter(edge => edge.type !== "annotation");
    const outgoing = getOutgoing(schema.edges, node.id).filter(edge => edge.type !== "annotation");

    if (node.type === "errorEvent") {
      if (outgoing.length > 0) {
        issues.push(issue("MF_ERROR_EVENT_OUTGOING", "Error Event cannot have outgoing flows.", { nodeId: node.id }));
      }
      if (!isInErrorScope(schema, node.id)) {
        issues.push(issue("MF_ERROR_EVENT_SCOPE", "Error Event must be inside an Error Handler Flow scope.", { nodeId: node.id }));
      }
    }

    if ((node.type === "breakEvent" || node.type === "continueEvent")) {
      if (!node.parentLoopId) {
        issues.push(issue("MF_LOOP_EVENT_SCOPE", "Break/Continue Event can only be placed inside a Loop.", { nodeId: node.id }));
      }
      if (outgoing.length > 0) {
        issues.push(issue("MF_LOOP_EVENT_OUTGOING", "Break/Continue Event cannot have outgoing flows.", { nodeId: node.id }));
      }
    }

    if (node.type === "parameter") {
      const name = node.config.parameter.name.trim();
      if (!name) {
        issues.push(issue("MF_PARAMETER_NODE_NAME", "Parameter node must configure parameter name.", { nodeId: node.id }));
      }
      if (incoming.some(edge => edge.type !== "annotation") || outgoing.some(edge => edge.type !== "annotation")) {
        issues.push(issue("MF_PARAMETER_SEQUENCE", "Parameter does not participate in Sequence Flow.", { nodeId: node.id }));
      }
    }

    if (node.type === "annotation" && (incoming.some(edge => edge.type !== "annotation") || outgoing.some(edge => edge.type !== "annotation"))) {
      issues.push(issue("MF_ANNOTATION_SEQUENCE", "Annotation does not participate in Sequence Flow.", { nodeId: node.id }));
    }

    if (node.type === "decision") {
      const decisionEdges = getOutgoing(schema.edges, node.id).filter(edge => edge.type === "decisionCondition");
      if (decisionEdges.length < 2) {
      issues.push(issue("MF_DECISION_OUTGOING", "Decision must have at least two outgoing flows.", { nodeId: node.id }));
      }
      const keys = decisionEdges.map(conditionKey).filter(Boolean);
      if (new Set(keys).size !== keys.length) {
        issues.push(issue("MF_DECISION_DUPLICATE_CONDITION", "Decision cannot have duplicate condition values.", { nodeId: node.id }));
      }
      const resultType = node.config.resultType ?? node.config.expression.expectedType?.name;
      if (resultType === "Boolean") {
        const booleanValues = new Set(decisionEdges.map(edge => edge.conditionValue?.kind === "boolean" ? edge.conditionValue.value : undefined));
        if (!booleanValues.has(true) || !booleanValues.has(false)) {
          issues.push(issue("MF_DECISION_BOOLEAN_BRANCHES", "Boolean Decision must have true and false condition flows.", { nodeId: node.id }));
        }
      }
      if (decisionEdges.some(edge => !edge.targetNodeId)) {
        issues.push(issue("MF_DECISION_BRANCH_TARGET", "Decision branch cannot miss target node.", { nodeId: node.id }));
      }
    }

    if (node.type === "objectTypeDecision") {
      const objectEdges = getOutgoing(schema.edges, node.id).filter(edge => edge.type === "objectTypeCondition");
      if (!hasText(node.config.inputObject)) {
        issues.push(issue("MF_OBJECT_TYPE_INPUT", "Object Type Decision must configure inputObject.", { nodeId: node.id }));
      }
      const keys = objectEdges.map(conditionKey).filter(Boolean);
      if (new Set(keys).size !== keys.length) {
        issues.push(issue("MF_OBJECT_TYPE_DUPLICATE_CONDITION", "Object Type Decision cannot have duplicate specialization branches.", { nodeId: node.id }));
      }
      if (!objectEdges.some(edge => edge.conditionValue?.kind === "objectType" && edge.conditionValue.entity === "empty")) {
        issues.push(issue("MF_OBJECT_TYPE_EMPTY_RECOMMENDED", "Object Type Decision should include an empty branch.", { nodeId: node.id }, "warning"));
      }
    }

    if (node.type === "merge" && incoming.length < 2) {
      issues.push(issue("MF_MERGE_INCOMING", "Merge must have at least two incoming flows.", { nodeId: node.id }));
    }
    if (node.type === "merge" && outgoing.filter(edge => edge.type === "sequence").length !== 1) {
      issues.push(issue("MF_MERGE_OUTGOING", "Merge must have exactly one outgoing Sequence Flow.", { nodeId: node.id }));
    }

    if (node.type === "loop") {
      if (incoming.length < 1 || outgoing.length < 1) {
        issues.push(issue("MF_LOOP_DEGREE", "Loop must have incoming and outgoing flow.", { nodeId: node.id }));
      }
      if ((node.config.loopType ?? "forEach") === "forEach" && !hasText(node.config.iterableVariableName)) {
        issues.push(issue("MF_LOOP_ITERABLE", "For Each Loop must configure a list variable.", { nodeId: node.id }));
      }
      if ((node.config.loopType ?? "forEach") === "forEach" && !hasText(node.config.itemVariableName)) {
        issues.push(issue("MF_LOOP_ITEM_NAME", "For Each Loop must configure loop object name.", { nodeId: node.id }));
      }
      if (node.config.loopType === "while" && !hasText(node.config.whileExpression?.text)) {
        issues.push(issue("MF_LOOP_WHILE_EXPRESSION", "While Loop must configure a Boolean expression.", { nodeId: node.id }));
      }
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
    if (["callJavaScriptAction", "callNanoflow", "synchronize"].includes(config.activityType)) {
      issues.push(issue("MF_NANOFLOW_ONLY", "This node is Nanoflow only and cannot be used in Microflow.", { nodeId: node.id }));
    }
    if (config.activityType === "queryExternalDatabase" && !hasText(config.connectorId)) {
      issues.push(issue("MF_CONNECTOR_REQUIRED", "Query External Database requires External Database Connector.", { nodeId: node.id }, "warning"));
    }
    if (config.activityType === "generateDocument") {
      issues.push(issue("MF_DEPRECATED_NODE", "Generate Document is deprecated and kept for compatibility only.", { nodeId: node.id }, "warning"));
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

    if (isExecutable(node) && !["startEvent"].includes(node.type) && incoming.length === 0 && !node.parentLoopId) {
      issues.push(issue("MF_EXECUTABLE_INCOMING", "Executable node should have an incoming control flow.", { nodeId: node.id }, "warning"));
    }
    if (isExecutable(node) && !isTerminal(node) && outgoing.filter(edge => edge.type !== "errorHandler").length === 0) {
      issues.push(issue("MF_EXECUTABLE_OUTGOING", "Executable non-terminal node should have a continuation path.", { nodeId: node.id }, "warning"));
    }
  }

  for (const node of schema.nodes.filter(isExecutable)) {
    if (node.type === "startEvent") {
      continue;
    }
    const start = startEvents[0];
    if (start && !reachableFromStart(schema, start.id).has(node.id) && !isInErrorScope(schema, node.id)) {
      issues.push(issue("MF_UNREACHABLE_NODE", "Executable node is not reachable from Start Event.", { nodeId: node.id }, "warning"));
    }
    if (!isTerminal(node) && !canReachTerminal(schema, node.id)) {
      issues.push(issue("MF_NO_TERMINAL_PATH", "Node cannot reach End Event or Error Event.", { nodeId: node.id }, "warning"));
    }
  }

  for (const edge of schema.edges.filter(edge => edge.type === "errorHandler")) {
    const source = nodesById.get(edge.sourceNodeId);
    if (source?.type === "activity" && source.config.errorHandling?.mode === "rollback") {
      issues.push(issue("MF_ERROR_HANDLER_ROLLBACK", "Error Handler Flow requires custom error handling mode, not rollback.", { edgeId: edge.id }));
    }
    if (!canReachTerminal({ ...schema, edges: schema.edges.filter(item => item.type !== "annotation") }, edge.targetNodeId)) {
      issues.push(issue("MF_ERROR_HANDLER_TERMINAL", "Error handling chain must eventually reach End Event or Error Event.", { edgeId: edge.id }));
    }
  }

  return issues;
}

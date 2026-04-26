import type { WorkflowEdgeJSON, WorkflowJSON, WorkflowNodeJSON } from "@flowgram-adapter/free-layout-editor";

import { flattenObjectCollection, toEditorGraph } from "../../adapters";
import type {
  MicroflowAuthoringSchema,
  MicroflowFlow,
  MicroflowObject,
  MicroflowSchema,
  MicroflowTraceFrame,
  MicroflowValidationIssue,
} from "../../schema";
import { microflowPortsToFlowGramPorts } from "./flowgram-port-factory";
import type { FlowGramMicroflowEdgeData, FlowGramMicroflowIssueIndex, FlowGramMicroflowNodeData } from "../FlowGramMicroflowTypes";

function titleForObject(object: MicroflowObject): string {
  if ("caption" in object && object.caption) {
    return object.caption;
  }
  if (object.kind === "actionActivity") {
    return object.action.caption || object.action.kind;
  }
  if (object.kind === "parameterObject") {
    return object.parameterName;
  }
  return object.kind;
}

function subtitleForObject(object: MicroflowObject): string | undefined {
  if (object.kind === "actionActivity") {
    return object.action.kind;
  }
  if (object.kind === "loopedActivity") {
    return object.iteratorVariableName ? `iterator: ${object.iteratorVariableName}` : "Loop";
  }
  return object.officialType;
}

function validationState(issues: MicroflowValidationIssue[]): "valid" | "warning" | "error" {
  if (issues.some(issue => issue.severity === "error")) {
    return "error";
  }
  if (issues.length > 0) {
    return "warning";
  }
  return "valid";
}

export function buildIssueIndex(issues: MicroflowValidationIssue[]): FlowGramMicroflowIssueIndex {
  const index: FlowGramMicroflowIssueIndex = new Map();
  for (const issue of issues) {
    const id = issue.objectId ?? issue.flowId;
    if (!id) {
      continue;
    }
    index.set(id, [...(index.get(id) ?? []), issue]);
  }
  return index;
}

function runtimeStateForObject(objectId: string, trace: MicroflowTraceFrame[] = []): FlowGramMicroflowNodeData["runtimeState"] {
  const frame = [...trace].reverse().find(item => item.objectId === objectId);
  if (!frame) {
    return "idle";
  }
  if (frame.status === "failed") {
    return "failed";
  }
  if (frame.status === "running") {
    return "running";
  }
  return "visited";
}

function runtimeStateForFlow(flowId: string, trace: MicroflowTraceFrame[] = []): FlowGramMicroflowEdgeData["runtimeState"] {
  return trace.some(item => item.incomingFlowId === flowId || item.outgoingFlowId === flowId) ? "visited" : "idle";
}

function flowLabel(flow: MicroflowFlow): string | undefined {
  if (flow.editor.label) {
    return flow.editor.label;
  }
  if (flow.kind === "sequence" && flow.isErrorHandler) {
    return "Error";
  }
  const firstCase = flow.kind === "sequence" ? flow.caseValues[0] : undefined;
  if (!firstCase) {
    return undefined;
  }
  if (firstCase.kind === "boolean") {
    return firstCase.value ? "是" : "否";
  }
  return firstCase.persistedValue;
}

export function authoringToFlowGram(
  schema: MicroflowSchema | MicroflowAuthoringSchema,
  issues: MicroflowValidationIssue[] = schema.validation?.issues ?? [],
  trace: MicroflowTraceFrame[] = [],
): WorkflowJSON {
  const graph = toEditorGraph({ ...schema, validation: { issues } });
  const objects = new Map(flattenObjectCollection(schema.objectCollection).map(object => [object.id, object]));
  const issueIndex = buildIssueIndex(issues);

  const nodes: WorkflowNodeJSON[] = graph.nodes.map(node => {
    const object = objects.get(node.objectId);
    const objectIssues = issueIndex.get(node.objectId) ?? [];
    const data: FlowGramMicroflowNodeData = {
      objectId: node.objectId,
      objectKind: object?.kind ?? node.kind,
      actionKind: object?.kind === "actionActivity" ? object.action.kind : undefined,
      title: object ? titleForObject(object) : node.title,
      subtitle: object ? subtitleForObject(object) : node.subtitle,
      documentation: object?.documentation?.text,
      officialType: object?.officialType ?? node.kind,
      disabled: Boolean(object && "disabled" in object && object.disabled),
      validationState: validationState(objectIssues),
      runtimeState: runtimeStateForObject(node.objectId, trace),
      issueCount: objectIssues.length,
    };
    return {
      id: node.objectId,
      type: data.objectKind,
      data,
      meta: {
        position: node.position,
        size: node.size,
        nodeDTOType: data.objectKind,
        useDynamicPort: true,
        defaultPorts: microflowPortsToFlowGramPorts(node.ports),
      },
    };
  });

  const edges: WorkflowEdgeJSON[] = graph.edges.map(edge => {
    const flow = schema.flows.find(item => item.id === edge.flowId);
    const flowIssues = issueIndex.get(edge.flowId) ?? [];
    const data: FlowGramMicroflowEdgeData = {
      flowId: edge.flowId,
      flowKind: flow?.kind ?? "sequence",
      edgeKind: edge.kind,
      isErrorHandler: flow?.kind === "sequence" ? flow.isErrorHandler : false,
      caseValues: flow?.kind === "sequence" ? flow.caseValues : [],
      label: flow ? flowLabel(flow) : edge.label,
      description: flow?.editor.description,
      runtimeState: runtimeStateForFlow(edge.flowId, trace),
      validationState: validationState(flowIssues),
    };
    return {
      id: edge.flowId,
      sourceNodeID: edge.sourceObjectId,
      targetNodeID: edge.targetObjectId,
      sourcePortID: edge.sourcePortId,
      targetPortID: edge.targetPortId,
      data,
    };
  });

  return { nodes, edges };
}

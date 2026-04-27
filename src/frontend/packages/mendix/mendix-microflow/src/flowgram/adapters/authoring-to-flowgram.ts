import type { WorkflowEdgeJSON, WorkflowJSON, WorkflowNodeJSON } from "@flowgram-adapter/free-layout-editor";

import { flattenObjectCollection, toEditorGraph } from "../../adapters/microflow-adapters";
import type { MicroflowTraceFrame } from "../../debug/trace-types";
import type {
  MicroflowAuthoringPersistedTraceFrame,
  MicroflowAuthoringSchema,
  MicroflowAction,
  MicroflowEditorPort,
  MicroflowFlow,
  MicroflowObject,
  MicroflowObjectCollection,
  MicroflowValidationIssue,
} from "../../schema/types";

type MicroflowFlowGramTraceRow = MicroflowTraceFrame | MicroflowAuthoringPersistedTraceFrame;
import { collectFlowsRecursive } from "../../schema/utils/object-utils";
import { flowCaseLabel } from "./flowgram-case-options";
import { validationStateFromIssues } from "./flowgram-validation-sync";
import type { FlowGramMicroflowEdgeData, FlowGramMicroflowIssueIndex, FlowGramMicroflowNodeData } from "../FlowGramMicroflowTypes";

function authoringPortsToFlowGramPorts(ports: MicroflowEditorPort[]) {
  return ports.map(port => ({
    type: port.direction,
    portID: port.id,
    disabled: port.cardinality === "none",
  }));
}

function titleForObject(object: MicroflowObject): string {
  if ("caption" in object && object.caption) {
    return object.caption;
  }
  if (object.kind === "actionActivity") {
    return object.action.caption || object.action.kind;
  }
  if (object.kind === "parameterObject") {
    return object.parameterName ?? object.parameterId;
  }
  return object.kind;
}

function subtitleForObject(object: MicroflowObject): string | undefined {
  if (object.kind === "actionActivity") {
    const output = outputSummaryForAction(object.action);
    const actionSummary = actionSubtitleSummary(object.action);
    return [object.action.editor.category, actionSummary, output ? `out: ${output}` : undefined].filter(Boolean).join(" · ");
  }
  if (object.kind === "loopedActivity") {
    return object.loopSource.kind === "iterableList" ? `iterator: ${object.loopSource.iteratorVariableName}` : "While condition";
  }
  return object.officialType;
}

function outputSummaryForAction(action: MicroflowAction): string | undefined {
  if (action.kind === "retrieve" || action.kind === "createObject") {
    return action.outputVariableName || undefined;
  }
  if (action.kind === "createVariable") {
    return action.variableName || undefined;
  }
  if (action.kind === "callMicroflow" && action.returnValue.storeResult) {
    return action.returnValue.outputVariableName || undefined;
  }
  if (action.kind === "restCall" && action.response.handling.kind !== "ignore") {
    return action.response.handling.outputVariableName || undefined;
  }
  return undefined;
}

function actionSubtitleSummary(action: MicroflowAction): string | undefined {
  if (action.kind === "restCall") {
    return `${action.request.method} ${action.request.urlExpression.raw}`;
  }
  if (action.kind === "logMessage") {
    return action.level;
  }
  if (action.kind === "callMicroflow") {
    return action.targetMicroflowQualifiedName || action.targetMicroflowId || undefined;
  }
  return undefined;
}

function collectCollectionObjectIds(collection: MicroflowObjectCollection): Set<string> {
  const ids = new Set<string>();
  for (const object of collection.objects) {
    ids.add(object.id);
    if (object.kind === "loopedActivity") {
      for (const childId of collectCollectionObjectIds(object.objectCollection)) {
        ids.add(childId);
      }
    }
  }
  return ids;
}

function loopSummaryForObject(
  object: MicroflowObject | undefined,
  schema: MicroflowAuthoringSchema,
): FlowGramMicroflowNodeData["loopSummary"] {
  if (object?.kind !== "loopedActivity") {
    return undefined;
  }
  const childObjects = flattenObjectCollection(object.objectCollection);
  const descendantIds = collectCollectionObjectIds(object.objectCollection);
  const flowCount = collectFlowsRecursive(schema).filter(flow => descendantIds.has(flow.originObjectId) && descendantIds.has(flow.destinationObjectId)).length;
  return {
    childCount: childObjects.length,
    flowCount,
    nestedLoopCount: childObjects.filter(child => child.kind === "loopedActivity").length,
    actionCount: childObjects.filter(child => child.kind === "actionActivity").length,
    eventCount: childObjects.filter(child => child.kind.endsWith("Event")).length,
    annotationCount: childObjects.filter(child => child.kind === "annotation").length,
  };
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

function runtimeStateForObject(objectId: string, trace: MicroflowFlowGramTraceRow[] = []): FlowGramMicroflowNodeData["runtimeState"] {
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
  if (frame.status === "skipped") {
    return "skipped";
  }
  return "success";
}

function runtimeStateForFlow(flow: MicroflowFlow | undefined, trace: MicroflowFlowGramTraceRow[] = []): FlowGramMicroflowEdgeData["runtimeState"] {
  if (!flow) {
    return "idle";
  }
  const visitedFrame = trace.find(item => item.incomingFlowId === flow.id || item.outgoingFlowId === flow.id);
  if (visitedFrame?.error && visitedFrame.outgoingFlowId === flow.id) {
    return "errorHandlerVisited";
  }
  if (visitedFrame?.selectedCaseValue && visitedFrame.outgoingFlowId === flow.id) {
    return "selectedCase";
  }
  if (visitedFrame) {
    return "visited";
  }
  const selectedSibling = trace.some(item => item.objectId === flow.originObjectId && item.selectedCaseValue && item.outgoingFlowId !== flow.id);
  return selectedSibling ? "skipped" : "idle";
}

export function authoringToFlowGram(
  schema: MicroflowAuthoringSchema,
  issues: MicroflowValidationIssue[] = schema.validation?.issues ?? [],
  trace: MicroflowFlowGramTraceRow[] = [],
): WorkflowJSON {
  const graph = toEditorGraph({ ...schema, validation: { issues } });
  const objects = new Map(flattenObjectCollection(schema.objectCollection).map(object => [object.id, object]));
  const issueIndex = buildIssueIndex(issues);
  const graphNodes = new Map(graph.nodes.map(node => [node.objectId, node]));
  const flowById = new Map(collectFlowsRecursive(schema).map(flow => [flow.id, flow]));

  const nodes: WorkflowNodeJSON[] = graph.nodes.map(node => {
    const object = objects.get(node.objectId);
    const objectIssues = issueIndex.get(node.objectId) ?? [];
    const parentNode = node.parentObjectId ? graphNodes.get(node.parentObjectId) : undefined;
    const position = parentNode
      ? {
          x: parentNode.position.x + node.position.x,
          y: parentNode.position.y + 76 + node.position.y,
        }
      : node.position;
    const data: FlowGramMicroflowNodeData = {
      objectId: node.objectId,
      objectKind: object?.kind ?? node.nodeKind,
      collectionId: node.collectionId,
      parentObjectId: node.parentObjectId,
      loopSource: object?.kind === "loopedActivity" ? object.loopSource : undefined,
      iteratorVariableName: object?.kind === "loopedActivity" && object.loopSource.kind === "iterableList" ? object.loopSource.iteratorVariableName : undefined,
      listVariableName: object?.kind === "loopedActivity" && object.loopSource.kind === "iterableList" ? object.loopSource.listVariableName : undefined,
      currentIndexVariableName: object?.kind === "loopedActivity" && object.loopSource.kind === "iterableList" ? object.loopSource.currentIndexVariableName : undefined,
      loopSummary: loopSummaryForObject(object, schema),
      actionKind: object?.kind === "actionActivity" ? object.action.kind : undefined,
      action: object?.kind === "actionActivity" ? object.action : undefined,
      availability: object?.kind === "actionActivity" ? object.action.editor.availability : undefined,
      availabilityReason: object?.kind === "actionActivity" ? object.action.editor.availabilityReason : undefined,
      title: object ? titleForObject(object) : node.title,
      subtitle: object ? subtitleForObject(object) : node.subtitle,
      documentation: object?.documentation,
      officialType: object?.officialType ?? node.nodeKind,
      disabled: Boolean(object && "disabled" in object && object.disabled),
      validationState: validationStateFromIssues(objectIssues),
      runtimeState: runtimeStateForObject(node.objectId, trace),
      issueCount: objectIssues.length,
    };
    return {
      id: node.objectId,
      type: data.objectKind,
      data,
      meta: {
        position,
        size: node.size,
        nodeDTOType: data.objectKind,
        useDynamicPort: true,
        defaultPorts: authoringPortsToFlowGramPorts(node.ports),
        parentObjectId: node.parentObjectId,
        collectionId: node.collectionId,
      },
    };
  });

  const edges: WorkflowEdgeJSON[] = graph.edges.map(edge => {
    const flow = flowById.get(edge.flowId);
    const flowIssues = issueIndex.get(edge.flowId) ?? [];
    const data: FlowGramMicroflowEdgeData = {
      flowId: edge.flowId,
      flowKind: flow?.kind ?? "sequence",
      edgeKind: edge.kind ?? edge.edgeKind,
      isErrorHandler: flow?.kind === "sequence" ? flow.isErrorHandler : false,
      caseValues: flow?.kind === "sequence" ? flow.caseValues : [],
      label: flow ? flowCaseLabel(flow) : edge.label,
      description: flow?.editor.description,
      runtimeState: runtimeStateForFlow(flow, trace),
      validationState: validationStateFromIssues(flowIssues),
    };
    return {
      id: edge.flowId,
      sourceNodeID: edge.sourceObjectId ?? edge.sourceNodeId,
      targetNodeID: edge.targetObjectId ?? edge.targetNodeId,
      sourcePortID: edge.sourcePortId,
      targetPortID: edge.targetPortId,
      data,
    };
  });

  return { nodes, edges };
}

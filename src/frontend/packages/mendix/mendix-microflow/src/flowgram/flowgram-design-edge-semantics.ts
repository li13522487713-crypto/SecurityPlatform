import type { WorkflowEdgeJSON, WorkflowJSON } from "@flowgram-adapter/free-layout-editor";

import type {
  MicroflowCaseValue,
  MicroflowObjectKind,
  MicroflowWorkflowEdgeJSON,
  MicroflowWorkflowNodeJSON,
} from "../schema";
import type { FlowGramMicroflowEdgeData, FlowGramMicroflowNodeData } from "./FlowGramMicroflowTypes";
import { forceOrthogonalLineKind } from "./FlowGramMicroflowTypes";

type EdgeKind = FlowGramMicroflowEdgeData["edgeKind"];

const MICROFLOW_ROOT_COLLECTION_ID = "root-collection";
const terminalKinds = new Set<string>(["endEvent", "errorEvent", "breakEvent", "continueEvent"]);
const singleOutgoingKinds = new Set<string>([
  "startEvent",
  "actionActivity",
  "exclusiveMerge",
  "loopedActivity",
  "parameterObject",
  "tryCatch",
  "errorHandler",
]);

function edgeId(edge: MicroflowWorkflowEdgeJSON | WorkflowEdgeJSON, index: number): string {
  const data = (edge as MicroflowWorkflowEdgeJSON).data as Partial<FlowGramMicroflowEdgeData> | undefined;
  return data?.flowId ?? (edge as { id?: string }).id ?? `flow-${index + 1}`;
}

function workflowNodeById(workflow: WorkflowJSON, nodeId?: string): MicroflowWorkflowNodeJSON | undefined {
  return ((workflow.nodes ?? []) as MicroflowWorkflowNodeJSON[]).find(node => String(node.id) === String(nodeId ?? ""));
}

function portId(value: unknown): string {
  return String(value ?? "");
}

function nodeKind(node: MicroflowWorkflowNodeJSON | undefined): MicroflowObjectKind | string | undefined {
  return String((node?.data as Partial<FlowGramMicroflowNodeData> | undefined)?.objectKind ?? node?.type ?? "");
}

function nodeCollectionId(node: MicroflowWorkflowNodeJSON | undefined): string {
  const data = node?.data as Partial<FlowGramMicroflowNodeData> | undefined;
  return String(data?.collectionId ?? node?.meta?.collectionId ?? MICROFLOW_ROOT_COLLECTION_ID);
}

function nodeParentObjectId(node: MicroflowWorkflowNodeJSON | undefined): string | undefined {
  const data = node?.data as Partial<FlowGramMicroflowNodeData> | undefined;
  return data?.parentObjectId ?? (node?.meta?.parentObjectId as string | undefined);
}

function isAnnotationEndpoint(sourceKind: string | undefined, targetKind: string | undefined): boolean {
  return sourceKind === "annotation" || targetKind === "annotation";
}

function caseValueKey(caseValue: MicroflowCaseValue): string {
  if (caseValue.kind === "boolean") {
    return `boolean:${caseValue.value}`;
  }
  if (caseValue.kind === "enumeration") {
    return `enumeration:${caseValue.enumerationQualifiedName}:${caseValue.value}`;
  }
  if (caseValue.kind === "inheritance") {
    return `inheritance:${caseValue.entityQualifiedName}`;
  }
  if (caseValue.kind === "expression") {
    return `expression:${caseValue.condition ?? caseValue.expression ?? ""}`;
  }
  return caseValue.kind;
}

function booleanCase(value: boolean): MicroflowCaseValue {
  return {
    kind: "boolean",
    officialType: "Microflows$EnumerationCase",
    value,
    persistedValue: value ? "true" : "false",
  };
}

function fallbackCase(): MicroflowCaseValue {
  return { kind: "fallback", officialType: "Microflows$NoCase" };
}

function emptyCase(): MicroflowCaseValue {
  return { kind: "empty", officialType: "Microflows$NoCase" };
}

function inferEdgeKind(edge: MicroflowWorkflowEdgeJSON, source: MicroflowWorkflowNodeJSON | undefined, target: MicroflowWorkflowNodeJSON | undefined): EdgeKind {
  const data = edge.data as Partial<FlowGramMicroflowEdgeData> | undefined;
  const existingKind = data?.edgeKind;
  const sourceKind = nodeKind(source);
  const targetKind = nodeKind(target);
  const sourcePort = portId(edge.sourcePortID).toLowerCase();

  if (existingKind === "annotation" || isAnnotationEndpoint(sourceKind, targetKind)) {
    return "annotation";
  }
  if (existingKind === "errorHandler" || sourcePort.includes("error")) {
    return "errorHandler";
  }
  if (
    existingKind === "loopBody"
    || sourcePort.includes("body")
    || (sourceKind === "loopedActivity" && nodeParentObjectId(target) === source?.id)
  ) {
    return "loopBody";
  }
  if (existingKind === "objectTypeCondition" || sourceKind === "inheritanceSplit") {
    return "objectTypeCondition";
  }
  if (existingKind === "decisionCondition" || sourceKind === "exclusiveSplit" || sourceKind === "inclusiveGateway") {
    return "decisionCondition";
  }
  return "sequence";
}

function usedCaseKeysForSource(workflow: WorkflowJSON, sourceNodeID: string, excludeFlowId: string): Set<string> {
  const used = new Set<string>();
  for (const edge of (workflow.edges ?? []) as MicroflowWorkflowEdgeJSON[]) {
    const data = edge.data as Partial<FlowGramMicroflowEdgeData> | undefined;
    const flowId = data?.flowId ?? edge.id;
    if (String(edge.sourceNodeID) !== sourceNodeID || flowId === excludeFlowId) {
      continue;
    }
    for (const caseValue of data?.caseValues ?? []) {
      used.add(caseValueKey(caseValue));
    }
  }
  return used;
}

function defaultCaseValuesForEdge(
  workflow: WorkflowJSON,
  edge: MicroflowWorkflowEdgeJSON,
  edgeKind: EdgeKind,
  sourceKind: string | undefined,
  flowId: string,
): MicroflowCaseValue[] {
  const data = edge.data as Partial<FlowGramMicroflowEdgeData> | undefined;
  if (data?.caseValues?.length) {
    return data.caseValues;
  }
  const sourcePort = portId(edge.sourcePortID).toLowerCase();
  if (edgeKind === "decisionCondition" && sourceKind === "exclusiveSplit") {
    if (sourcePort.includes("false")) {
      return [booleanCase(false)];
    }
    if (sourcePort.includes("true")) {
      return [booleanCase(true)];
    }
    const used = usedCaseKeysForSource(workflow, String(edge.sourceNodeID ?? ""), flowId);
    if (!used.has("boolean:true")) {
      return [booleanCase(true)];
    }
    if (!used.has("boolean:false")) {
      return [booleanCase(false)];
    }
    return [fallbackCase()];
  }
  if (edgeKind === "objectTypeCondition") {
    return [emptyCase()];
  }
  return [];
}

export function normalizeMicroflowDesignEdges(workflow: WorkflowJSON): MicroflowWorkflowEdgeJSON[] {
  const normalized: MicroflowWorkflowEdgeJSON[] = [];
  for (const [index, edge] of ((workflow.edges ?? []) as MicroflowWorkflowEdgeJSON[]).entries()) {
    const id = edgeId(edge, index);
    const source = workflowNodeById(workflow, String(edge.sourceNodeID));
    const target = workflowNodeById(workflow, String(edge.targetNodeID));
    const sourceKind = nodeKind(source);
    const targetKind = nodeKind(target);
    const edgeKind = inferEdgeKind(edge, source, target);
    const data = edge.data as Partial<FlowGramMicroflowEdgeData> | undefined;
    const edgeWorkflow = { ...workflow, edges: normalized } as WorkflowJSON;
    normalized.push({
      ...edge,
      id,
      data: {
        ...data,
        flowId: id,
        flowKind: edgeKind === "annotation" ? "annotation" : "sequence",
        edgeKind,
        isErrorHandler: edgeKind === "errorHandler",
        caseValues: defaultCaseValuesForEdge(edgeWorkflow, edge, edgeKind, sourceKind, id),
        lineKind: forceOrthogonalLineKind(data?.lineKind),
        sourceNodeId: String(edge.sourceNodeID ?? ""),
        sourceObjectKind: sourceKind as MicroflowObjectKind | undefined,
        sourcePortId: portId(edge.sourcePortID),
        targetNodeId: String(edge.targetNodeID ?? ""),
        targetObjectKind: targetKind as MicroflowObjectKind | undefined,
        targetPortId: portId(edge.targetPortID),
        validationState: data?.validationState ?? "valid",
        runtimeState: data?.runtimeState ?? "idle",
      },
    });
  }
  return normalized;
}

function isSameStructuralEdge(left: WorkflowEdgeJSON, right: WorkflowEdgeJSON): boolean {
  return String(left.sourceNodeID ?? "") === String(right.sourceNodeID ?? "")
    && String(left.sourcePortID ?? "") === String(right.sourcePortID ?? "")
    && String(left.targetNodeID ?? "") === String(right.targetNodeID ?? "")
    && String(left.targetPortID ?? "") === String(right.targetPortID ?? "");
}

function edgeBusinessKind(edge: WorkflowEdgeJSON): EdgeKind {
  return ((edge as MicroflowWorkflowEdgeJSON).data as Partial<FlowGramMicroflowEdgeData> | undefined)?.edgeKind ?? "sequence";
}

function hasDuplicateBooleanCase(workflow: WorkflowJSON, edge: WorkflowEdgeJSON): boolean {
  const data = (edge as MicroflowWorkflowEdgeJSON).data as Partial<FlowGramMicroflowEdgeData> | undefined;
  const current = data?.caseValues?.[0];
  if (current?.kind !== "boolean") {
    return false;
  }
  const currentId = data?.flowId ?? (edge as { id?: string }).id;
  return ((workflow.edges ?? []) as MicroflowWorkflowEdgeJSON[]).some(candidate => {
    const candidateData = candidate.data as Partial<FlowGramMicroflowEdgeData> | undefined;
    const candidateId = candidateData?.flowId ?? candidate.id;
    const candidateCase = candidateData?.caseValues?.[0];
    return candidateId !== currentId
      && String(candidate.sourceNodeID) === String(edge.sourceNodeID)
      && candidateData?.edgeKind === "decisionCondition"
      && candidateCase?.kind === "boolean"
      && candidateCase.value === current.value;
  });
}

function countRuntimeOutgoing(workflow: WorkflowJSON, sourceNodeID: string, excludeEdge?: WorkflowEdgeJSON): number {
  return ((workflow.edges ?? []) as WorkflowEdgeJSON[]).filter(candidate => {
    if (excludeEdge && candidate === excludeEdge) {
      return false;
    }
    const kind = edgeBusinessKind(candidate);
    return String(candidate.sourceNodeID ?? "") === sourceNodeID
      && kind !== "annotation"
      && kind !== "errorHandler"
      && kind !== "loopBody";
  }).length;
}

export function isMicroflowDesignEdgeBusinessValid(workflow: WorkflowJSON, edge: WorkflowEdgeJSON): boolean {
  const source = workflowNodeById(workflow, String(edge.sourceNodeID));
  const target = workflowNodeById(workflow, String(edge.targetNodeID));
  if (!source || !target || source.id === target.id) {
    return false;
  }
  const sourceKind = nodeKind(source);
  const targetKind = nodeKind(target);
  if (terminalKinds.has(String(sourceKind)) || targetKind === "startEvent") {
    return false;
  }

  const matchingEdges = ((workflow.edges ?? []) as WorkflowEdgeJSON[]).filter(candidate => isSameStructuralEdge(candidate, edge));
  if (matchingEdges.length > 1) {
    return false;
  }

  const edgeKind = edgeBusinessKind(edge);
  const sourceCollection = nodeCollectionId(source);
  const targetCollection = nodeCollectionId(target);
  const sourceParentObjectId = nodeParentObjectId(source);
  const targetParentObjectId = nodeParentObjectId(target);
  const isLoopBodyEntry = edgeKind === "loopBody"
    && sourceKind === "loopedActivity"
    && nodeParentObjectId(target) === source.id;
  if (edgeKind !== "annotation" && !isLoopBodyEntry && sourceCollection !== targetCollection) {
    return false;
  }
  if (edgeKind !== "annotation" && !isLoopBodyEntry && String(sourceParentObjectId ?? "") !== String(targetParentObjectId ?? "")) {
    return false;
  }
  if (edgeKind === "loopBody" && !isLoopBodyEntry) {
    return false;
  }
  if (edgeKind === "decisionCondition" && sourceKind !== "exclusiveSplit" && sourceKind !== "inclusiveGateway") {
    return false;
  }
  if (edgeKind === "objectTypeCondition" && sourceKind !== "inheritanceSplit") {
    return false;
  }
  if (hasDuplicateBooleanCase(workflow, edge)) {
    return false;
  }
  if (singleOutgoingKinds.has(String(sourceKind)) && countRuntimeOutgoing(workflow, source.id, edge) > 0 && edgeKind === "sequence") {
    return false;
  }
  return true;
}

export function findLoopParentAtPoint(workflow: WorkflowJSON, point: { x: number; y: number }): string | undefined {
  const loops = ((workflow.nodes ?? []) as MicroflowWorkflowNodeJSON[])
    .filter(node => nodeKind(node) === "loopedActivity")
    .map(node => {
      const position = node.meta?.position ?? { x: 0, y: 0 };
      const size = node.meta?.size ?? { width: 320, height: 190 };
      return {
        node,
        left: position.x - size.width / 2,
        top: position.y - size.height / 2,
        right: position.x + size.width / 2,
        bottom: position.y + size.height / 2,
      };
    })
    .filter(box => point.x >= box.left && point.x <= box.right && point.y >= box.top && point.y <= box.bottom)
    .sort((a, b) => (a.right - a.left) * (a.bottom - a.top) - (b.right - b.left) * (b.bottom - b.top));

  return loops[0]?.node.id;
}

export function canDropRegistryObjectKindInLoop(kind: string | undefined, parentLoopObjectId: string | undefined): boolean {
  const insideLoop = Boolean(parentLoopObjectId);
  if ((kind === "breakEvent" || kind === "continueEvent") && !insideLoop) {
    return false;
  }
  if (insideLoop && (kind === "startEvent" || kind === "endEvent" || kind === "parameterObject")) {
    return false;
  }
  return true;
}

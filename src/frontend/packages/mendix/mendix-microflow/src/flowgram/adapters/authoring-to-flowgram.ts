import type { WorkflowEdgeJSON, WorkflowJSON, WorkflowNodeJSON } from "@flowgram-adapter/free-layout-editor";

import { flattenObjectCollection, toEditorGraph } from "../../adapters";
import type {
  MicroflowAuthoringSchema,
  MicroflowObject,
  MicroflowObjectCollection,
  MicroflowSchema,
  MicroflowTraceFrame,
  MicroflowValidationIssue,
} from "../../schema";
import { collectFlowsRecursive } from "../../schema/utils/object-utils";
import { flowCaseLabel } from "./flowgram-case-options";
import { microflowPortsToFlowGramPorts } from "./flowgram-port-factory";
import { validationStateFromIssues } from "./flowgram-validation-sync";
import type { FlowGramMicroflowEdgeData, FlowGramMicroflowIssueIndex, FlowGramMicroflowNodeData } from "../FlowGramMicroflowTypes";

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
    return object.action.kind;
  }
  if (object.kind === "loopedActivity") {
    return object.loopSource.kind === "iterableList" ? `iterator: ${object.loopSource.iteratorVariableName}` : "While condition";
  }
  return object.officialType;
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
  schema: MicroflowSchema | MicroflowAuthoringSchema,
): FlowGramMicroflowNodeData["loopSummary"] {
  if (object?.kind !== "loopedActivity") {
    return undefined;
  }
  const childObjects = flattenObjectCollection(object.objectCollection);
  const descendantIds = collectCollectionObjectIds(object.objectCollection);
  const flowCount = collectFlowsRecursive(schema as MicroflowSchema).filter(flow => descendantIds.has(flow.originObjectId) && descendantIds.has(flow.destinationObjectId)).length;
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

export function authoringToFlowGram(
  schema: MicroflowSchema | MicroflowAuthoringSchema,
  issues: MicroflowValidationIssue[] = schema.validation?.issues ?? [],
  trace: MicroflowTraceFrame[] = [],
): WorkflowJSON {
  const graph = toEditorGraph({ ...schema, validation: { issues } });
  const objects = new Map(flattenObjectCollection(schema.objectCollection).map(object => [object.id, object]));
  const issueIndex = buildIssueIndex(issues);
  const graphNodes = new Map(graph.nodes.map(node => [node.objectId, node]));

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
        defaultPorts: microflowPortsToFlowGramPorts(node.ports),
        parentObjectId: node.parentObjectId,
        collectionId: node.collectionId,
      },
    };
  });

  const edges: WorkflowEdgeJSON[] = graph.edges.map(edge => {
    const flow = collectFlowsRecursive(schema as MicroflowSchema).find(item => item.id === edge.flowId);
    const flowIssues = issueIndex.get(edge.flowId) ?? [];
    const data: FlowGramMicroflowEdgeData = {
      flowId: edge.flowId,
      flowKind: flow?.kind ?? "sequence",
      edgeKind: edge.kind ?? edge.edgeKind,
      isErrorHandler: flow?.kind === "sequence" ? flow.isErrorHandler : false,
      caseValues: flow?.kind === "sequence" ? flow.caseValues : [],
      label: flow ? flowCaseLabel(flow) : edge.label,
      description: flow?.editor.description,
      runtimeState: runtimeStateForFlow(edge.flowId, trace),
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

import type { WorkflowEdgeJSON, WorkflowJSON } from "@flowgram-adapter/free-layout-editor";

import { toEditorGraph } from "../../adapters";
import type {
  MicroflowEditorGraphPatch,
  MicroflowEditorPort,
  MicroflowFlow,
  MicroflowSchema,
  MicroflowSize,
} from "../../schema";
import type { FlowGramMicroflowSelection } from "../FlowGramMicroflowTypes";
import { createMicroflowFlowFromPorts } from "./flowgram-edge-factory";

function edgeKey(edge: Pick<WorkflowEdgeJSON, "sourceNodeID" | "targetNodeID" | "sourcePortID" | "targetPortID">): string {
  return [edge.sourceNodeID, edge.sourcePortID ?? "", edge.targetNodeID, edge.targetPortID ?? ""].map(value => String(value ?? "")).join("::");
}

function portById(schema: MicroflowSchema, portId?: string): MicroflowEditorPort | undefined {
  if (!portId) {
    return undefined;
  }
  return toEditorGraph(schema).nodes.flatMap(node => node.ports).find(port => port.id === portId);
}

export function flowGramPositionPatch(schema: MicroflowSchema, json: WorkflowJSON): MicroflowEditorGraphPatch {
  const nodes = new Map(toEditorGraph(schema).nodes.map(node => [node.objectId, node]));
  const movedNodes: NonNullable<MicroflowEditorGraphPatch["movedNodes"]> = [];
  const resizedNodes: NonNullable<MicroflowEditorGraphPatch["resizedNodes"]> = [];
  for (const node of json.nodes ?? []) {
    const previous = nodes.get(node.id);
    const position = node.meta?.position;
    const size = (node.meta as { size?: MicroflowSize } | undefined)?.size;
    if (!previous) {
      continue;
    }
    if (position && (previous.position.x !== position.x || previous.position.y !== position.y)) {
      movedNodes.push({ objectId: node.id, position });
    }
    if (size && (previous.size.width !== size.width || previous.size.height !== size.height)) {
      resizedNodes.push({ objectId: node.id, size });
    }
  }
  return {
    movedNodes,
    resizedNodes,
  };
}

export function flowGramSelectionPatch(selection: FlowGramMicroflowSelection): MicroflowEditorGraphPatch {
  return {
    selectedObjectId: selection.objectId,
    selectedFlowId: selection.flowId,
  };
}

export function flowGramViewportPatch(viewport?: MicroflowEditorGraphPatch["viewport"]): MicroflowEditorGraphPatch {
  return viewport ? { viewport } : {};
}

export function findNewFlowGramEdge(schema: MicroflowSchema, json: WorkflowJSON): WorkflowEdgeJSON | undefined {
  const current = new Set(
    toEditorGraph(schema).edges.map(edge =>
      edgeKey({
        sourceNodeID: edge.sourceObjectId ?? edge.sourceNodeId,
        sourcePortID: edge.sourcePortId,
        targetNodeID: edge.targetObjectId ?? edge.targetNodeId,
        targetPortID: edge.targetPortId,
      }),
    ),
  );
  return (json.edges ?? []).find(edge => {
    const data = (edge as WorkflowEdgeJSON & { data?: { flowId?: string } }).data;
    return !data?.flowId && !current.has(edgeKey(edge));
  });
}

export function createFlowFromFlowGramEdge(schema: MicroflowSchema, edge: WorkflowEdgeJSON): MicroflowFlow | undefined {
  const sourcePort = portById(schema, edge.sourcePortID === undefined ? undefined : String(edge.sourcePortID));
  const targetPort = portById(schema, edge.targetPortID === undefined ? undefined : String(edge.targetPortID));
  if (!sourcePort || !targetPort) {
    return undefined;
  }
  return createMicroflowFlowFromPorts(schema, sourcePort, targetPort);
}

export function findDeletedFlowId(schema: MicroflowSchema, json: WorkflowJSON): string | undefined {
  const flowIds = new Set(
    (json.edges ?? [])
      .map(edge => (edge as WorkflowEdgeJSON & { data?: { flowId?: string } }).data?.flowId)
      .filter((id): id is string => Boolean(id)),
  );
  return schema.flows.find(flow => !flowIds.has(flow.id))?.id;
}

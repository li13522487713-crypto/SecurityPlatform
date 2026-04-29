import type { WorkflowEdgeJSON, WorkflowJSON } from "@flowgram-adapter/free-layout-editor";

import { toEditorGraph } from "../../adapters";
import type {
  MicroflowEditorGraphPatch,
  MicroflowFlow,
  MicroflowSchema,
  MicroflowSize,
} from "../../schema";
import { collectFlowsRecursive } from "../../schema/utils/object-utils";
import type { FlowGramMicroflowSelection } from "../FlowGramMicroflowTypes";
import { mapFlowGramEdgeToMicroflowFlow } from "./flowgram-edge-mapping";
import { MICROFLOW_GRID_SIZE, snapMicroflowPoint } from "./flowgram-coordinate";

function edgeKey(edge: Pick<WorkflowEdgeJSON, "sourceNodeID" | "targetNodeID" | "sourcePortID" | "targetPortID">): string {
  return [edge.sourceNodeID, edge.sourcePortID ?? "", edge.targetNodeID, edge.targetPortID ?? ""].map(value => String(value ?? "")).join("::");
}

export function flowGramPositionPatch(
  schema: MicroflowSchema,
  json: WorkflowJSON,
  options?: { gridEnabled?: boolean; gridSize?: number },
): MicroflowEditorGraphPatch {
  const nodes = new Map(toEditorGraph(schema).nodes.map(node => [node.objectId, node]));
  const absolutePositionByObjectId = new Map<string, { x: number; y: number }>();
  for (const node of json.nodes ?? []) {
    const rawPosition = node.meta?.position;
    if (rawPosition) {
      absolutePositionByObjectId.set(String(node.id), { x: rawPosition.x, y: rawPosition.y });
    }
  }
  const movedNodes: NonNullable<MicroflowEditorGraphPatch["movedNodes"]> = [];
  const resizedNodes: NonNullable<MicroflowEditorGraphPatch["resizedNodes"]> = [];
  for (const node of json.nodes ?? []) {
    const previous = nodes.get(node.id);
    const rawPosition = node.meta?.position;
    const size = (node.meta as { size?: MicroflowSize } | undefined)?.size;
    if (!previous) {
      continue;
    }
    const parent = previous.parentObjectId ? nodes.get(previous.parentObjectId) : undefined;
    const parentPosition = previous.parentObjectId
      ? absolutePositionByObjectId.get(previous.parentObjectId) ?? parent?.position
      : undefined;
    const position = rawPosition && parent
      ? {
          x: rawPosition.x - (parentPosition?.x ?? parent.position.x),
          y: rawPosition.y - (parentPosition?.y ?? parent.position.y) - 76,
        }
      : rawPosition;
    const rawPositionChanged = position && (previous.position.x !== position.x || previous.position.y !== position.y);
    if (rawPositionChanged) {
      const nextPosition = options?.gridEnabled
        ? snapMicroflowPoint(position, options.gridSize ?? MICROFLOW_GRID_SIZE)
        : position;
      if (previous.position.x !== nextPosition.x || previous.position.y !== nextPosition.y) {
        movedNodes.push({ objectId: node.id, position: nextPosition });
      }
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
    selectedCollectionId: selection.collectionId,
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

export function findDeletedObjectId(schema: MicroflowSchema, json: WorkflowJSON): string | undefined {
  const nodeIds = new Set((json.nodes ?? []).map(node => String(node.id)));
  return toEditorGraph(schema).nodes.find(node => !nodeIds.has(node.objectId))?.objectId;
}

export function createFlowFromFlowGramEdge(schema: MicroflowSchema, edge: WorkflowEdgeJSON): MicroflowFlow | undefined {
  return mapFlowGramEdgeToMicroflowFlow(schema, edge);
}

export function findDeletedFlowId(schema: MicroflowSchema, json: WorkflowJSON): string | undefined {
  const flowIds = new Set(
    (json.edges ?? [])
      .map(edge => (edge as WorkflowEdgeJSON & { data?: { flowId?: string } }).data?.flowId)
      .filter((id): id is string => Boolean(id)),
  );
  return collectFlowsRecursive(schema).find(flow => !flowIds.has(flow.id))?.id;
}

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
import { LOOP_HEADER_OFFSET_PX, MICROFLOW_GRID_SIZE, snapMicroflowPoint } from "./flowgram-coordinate";
import {
  normalizeFlowGramEdgeIdentity,
  stableFlowGramEdgeStructuralKey,
  toMicroflowFlowId,
  toMicroflowObjectId,
  type FlowGramComparableEdge,
} from "./flowgram-identity";

export function flowGramPositionPatch(
  schema: MicroflowSchema,
  json: WorkflowJSON,
  options?: { gridEnabled?: boolean; gridSize?: number },
): MicroflowEditorGraphPatch {
  const nodes = new Map(toEditorGraph(schema).nodes.map(node => [node.objectId, node]));
  const absolutePositionByObjectId = new Map<string, { x: number; y: number }>();
  for (const node of json.nodes ?? []) {
    const objectId = toMicroflowObjectId(node.id);
    const rawPosition = node.meta?.position;
    if (rawPosition) {
      absolutePositionByObjectId.set(objectId, rawPosition);
    }
  }
  const movedNodes: NonNullable<MicroflowEditorGraphPatch["movedNodes"]> = [];
  const resizedNodes: NonNullable<MicroflowEditorGraphPatch["resizedNodes"]> = [];
  for (const node of json.nodes ?? []) {
    const objectId = toMicroflowObjectId(node.id);
    const previous = nodes.get(objectId);
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
          y: rawPosition.y - (parentPosition?.y ?? parent.position.y) - LOOP_HEADER_OFFSET_PX,
        }
      : rawPosition;
    const rawPositionChanged = position && (previous.position.x !== position.x || previous.position.y !== position.y);
    if (rawPositionChanged) {
      const nextPosition = options?.gridEnabled
        ? snapMicroflowPoint(position, options.gridSize ?? MICROFLOW_GRID_SIZE)
        : position;
      if (previous.position.x !== nextPosition.x || previous.position.y !== nextPosition.y) {
        movedNodes.push({ objectId, position: nextPosition });
      }
    }
    if (size && (previous.size.width !== size.width || previous.size.height !== size.height)) {
      resizedNodes.push({ objectId, size });
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
    selectedObjectIds: selection.objectIds,
    selectedFlowIds: selection.flowIds,
    selectionMode: selection.mode,
  };
}

export function flowGramViewportPatch(viewport?: MicroflowEditorGraphPatch["viewport"]): MicroflowEditorGraphPatch {
  return viewport ? { viewport } : {};
}

export function findNewFlowGramEdge(schema: MicroflowSchema, json: WorkflowJSON): WorkflowEdgeJSON | undefined {
  const current = new Set(
    toEditorGraph(schema).edges.map(edge =>
      stableFlowGramEdgeStructuralKey({
        sourceObjectId: edge.sourceObjectId ?? toMicroflowObjectId(edge.sourceNodeId),
        sourcePortId: edge.sourcePortId,
        targetObjectId: edge.targetObjectId ?? toMicroflowObjectId(edge.targetNodeId),
        targetPortId: edge.targetPortId,
      }),
    ),
  );
  return (json.edges ?? []).find(edge => {
    const stableEdge = normalizeFlowGramEdgeIdentity(edge as WorkflowEdgeJSON & FlowGramComparableEdge);
    if (!stableEdge) {
      return false;
    }
    const flowId = stableEdge.flowId ? toMicroflowFlowId(stableEdge.flowId) : undefined;
    if (flowId && collectFlowsRecursive(schema).some(flow => flow.id === flowId)) {
      return false;
    }
    return !current.has(stableFlowGramEdgeStructuralKey(stableEdge));
  });
}

export function findDeletedObjectId(schema: MicroflowSchema, json: WorkflowJSON): string | undefined {
  const nodeIds = new Set((json.nodes ?? []).map(node => toMicroflowObjectId(node.id)));
  return toEditorGraph(schema).nodes.find(node => !nodeIds.has(node.objectId))?.objectId;
}

export function createFlowFromFlowGramEdge(schema: MicroflowSchema, edge: WorkflowEdgeJSON): MicroflowFlow | undefined {
  return mapFlowGramEdgeToMicroflowFlow(schema, edge);
}

export function findDeletedFlowId(schema: MicroflowSchema, json: WorkflowJSON): string | undefined {
  const flowIds = new Set((json.edges ?? [])
    .map(edge => {
      const stableEdge = normalizeFlowGramEdgeIdentity(edge as WorkflowEdgeJSON & FlowGramComparableEdge);
      return stableEdge?.flowId ? toMicroflowFlowId(stableEdge.flowId) : undefined;
    })
    .filter((id): id is string => Boolean(id)));
  const edgeKeys = new Set(
    (json.edges ?? [])
      .map(edge => normalizeFlowGramEdgeIdentity(edge as WorkflowEdgeJSON & FlowGramComparableEdge))
      .filter((edge): edge is NonNullable<ReturnType<typeof normalizeFlowGramEdgeIdentity>> => Boolean(edge))
      .map(stableFlowGramEdgeStructuralKey),
  );
  return collectFlowsRecursive(schema).find(flow => {
    if (flowIds.has(flow.id)) {
      return false;
    }
    const graphEdge = toEditorGraph(schema).edges.find(item => item.flowId === flow.id);
    if (!graphEdge) {
      return true;
    }
    const key = stableFlowGramEdgeStructuralKey({
      sourceObjectId: graphEdge.sourceObjectId ?? toMicroflowObjectId(graphEdge.sourceNodeId),
      sourcePortId: graphEdge.sourcePortId,
      targetObjectId: graphEdge.targetObjectId ?? toMicroflowObjectId(graphEdge.targetNodeId),
      targetPortId: graphEdge.targetPortId,
    });
    return !edgeKeys.has(key);
  })?.id;
}

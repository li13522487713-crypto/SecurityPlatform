import type { WorkflowEdgeJSON, WorkflowJSON } from "@flowgram-adapter/free-layout-editor";

import { toEditorGraph } from "../../adapters";
import type { MicroflowEditorGraphPatch, MicroflowEditorPort, MicroflowFlow, MicroflowSchema } from "../../schema";
import { createMicroflowFlowFromPorts } from "./flowgram-edge-factory";

function edgeKey(edge: Pick<WorkflowEdgeJSON, "sourceNodeID" | "targetNodeID" | "sourcePortID" | "targetPortID">): string {
  return [edge.sourceNodeID, edge.sourcePortID ?? "", edge.targetNodeID, edge.targetPortID ?? ""].join("::");
}

function portById(schema: MicroflowSchema, portId?: string): MicroflowEditorPort | undefined {
  if (!portId) {
    return undefined;
  }
  return toEditorGraph(schema).nodes.flatMap(node => node.ports).find(port => port.id === portId);
}

export function flowGramPositionPatch(schema: MicroflowSchema, json: WorkflowJSON): MicroflowEditorGraphPatch {
  const nodes = new Map(toEditorGraph(schema).nodes.map(node => [node.objectId, node]));
  return {
    movedNodes: (json.nodes ?? [])
      .map(node => {
        const previous = nodes.get(node.id);
        const position = node.meta?.position;
        if (!previous || !position) {
          return undefined;
        }
        if (previous.position.x === position.x && previous.position.y === position.y) {
          return undefined;
        }
        return { objectId: node.id, position };
      })
      .filter((item): item is NonNullable<typeof item> => Boolean(item)),
  };
}

export function findNewFlowGramEdge(schema: MicroflowSchema, json: WorkflowJSON): WorkflowEdgeJSON | undefined {
  const current = new Set(
    toEditorGraph(schema).edges.map(edge =>
      edgeKey({
        sourceNodeID: edge.sourceObjectId,
        sourcePortID: edge.sourcePortId,
        targetNodeID: edge.targetObjectId,
        targetPortID: edge.targetPortId,
      }),
    ),
  );
  return (json.edges ?? []).find(edge => {
    const data = edge.data as { flowId?: string } | undefined;
    return !data?.flowId && !current.has(edgeKey(edge));
  });
}

export function createFlowFromFlowGramEdge(schema: MicroflowSchema, edge: WorkflowEdgeJSON): MicroflowFlow | undefined {
  const sourcePort = portById(schema, edge.sourcePortID);
  const targetPort = portById(schema, edge.targetPortID);
  if (!sourcePort || !targetPort) {
    return undefined;
  }
  return createMicroflowFlowFromPorts(schema, sourcePort, targetPort);
}

export function findDeletedFlowId(schema: MicroflowSchema, json: WorkflowJSON): string | undefined {
  const flowIds = new Set(
    (json.edges ?? [])
      .map(edge => (edge.data as { flowId?: string } | undefined)?.flowId)
      .filter((id): id is string => Boolean(id)),
  );
  return schema.flows.find(flow => !flowIds.has(flow.id))?.id;
}


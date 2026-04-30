import type { WorkflowEdgeJSON, WorkflowJSON } from "@flowgram-adapter/free-layout-editor";

export interface FlowGramComparableEdge {
  id?: string | number;
  flowId?: string;
  sourceNodeID?: string | number;
  targetNodeID?: string | number;
  sourcePortID?: string | number;
  targetPortID?: string | number;
  sourceNodeId?: string;
  targetNodeId?: string;
  sourceObjectId?: string;
  targetObjectId?: string;
  sourcePortId?: string;
  targetPortId?: string;
  data?: {
    flowId?: string;
  };
}

export interface FlowGramStableEdgeIdentity {
  flowId?: string;
  sourceObjectId: string;
  sourcePortId?: string;
  targetObjectId: string;
  targetPortId?: string;
}

export function toFlowGramNodeId(objectId: string): string {
  return objectId;
}

export function toMicroflowObjectId(nodeId: string | number | undefined): string {
  const normalized = String(nodeId ?? "");
  return normalized.startsWith("node-") ? normalized.slice(5) : normalized;
}

export function toFlowGramEdgeId(flowId: string): string {
  return flowId;
}

export function toMicroflowFlowId(edgeEntityId: string | number | undefined): string {
  const normalized = String(edgeEntityId ?? "");
  return normalized.startsWith("edge-") ? normalized.slice(5) : normalized;
}

export function normalizeFlowGramEdgeIdentity(edge: FlowGramComparableEdge): FlowGramStableEdgeIdentity | undefined {
  const sourceObjectId = edge.sourceObjectId ?? toMicroflowObjectId(edge.sourceNodeID ?? edge.sourceNodeId);
  const targetObjectId = edge.targetObjectId ?? toMicroflowObjectId(edge.targetNodeID ?? edge.targetNodeId);
  if (!sourceObjectId || !targetObjectId) {
    return undefined;
  }
  return {
    flowId: edge.flowId ?? edge.data?.flowId ?? toMicroflowFlowId(edge.id),
    sourceObjectId,
    sourcePortId: edge.sourcePortId ?? (edge.sourcePortID === undefined ? undefined : String(edge.sourcePortID)),
    targetObjectId,
    targetPortId: edge.targetPortId ?? (edge.targetPortID === undefined ? undefined : String(edge.targetPortID)),
  };
}

export function stableFlowGramEdgeKey(edge: FlowGramStableEdgeIdentity): string {
  return [
    edge.sourceObjectId,
    edge.sourcePortId ?? "",
    edge.targetObjectId,
    edge.targetPortId ?? "",
    edge.flowId ?? "",
  ].join("::");
}

export function stableFlowGramEdgeStructuralKey(edge: FlowGramStableEdgeIdentity): string {
  return [
    edge.sourceObjectId,
    edge.sourcePortId ?? "",
    edge.targetObjectId,
    edge.targetPortId ?? "",
  ].join("::");
}

export function flowGramNodeIdentitySignature(json: WorkflowJSON | undefined): string {
  if (!json) {
    return "";
  }
  return (json.nodes ?? [])
    .map(node => {
      const meta = node.meta as { parentObjectId?: string; collectionId?: string } | undefined;
      return [
        toMicroflowObjectId(node.id),
        String(node.type ?? ""),
        meta?.parentObjectId ?? "",
        meta?.collectionId ?? "",
      ].join("::");
    })
    .sort()
    .join("|");
}

export function flowGramEdgeIdentitySignature(json: WorkflowJSON | undefined): string {
  if (!json) {
    return "";
  }
  return (json.edges ?? [])
    .map(edge => normalizeFlowGramEdgeIdentity(edge as WorkflowEdgeJSON & FlowGramComparableEdge))
    .filter((edge): edge is FlowGramStableEdgeIdentity => Boolean(edge))
    .map(stableFlowGramEdgeKey)
    .sort()
    .join("|");
}

export function flowGramPositionSignature(json: WorkflowJSON | undefined): string {
  if (!json) {
    return "";
  }
  return (json.nodes ?? [])
    .map(node => {
      const position = node.meta?.position;
      const size = (node.meta as { size?: { width: number; height: number } } | undefined)?.size;
      return [
        toMicroflowObjectId(node.id),
        position?.x ?? "",
        position?.y ?? "",
        size?.width ?? "",
        size?.height ?? "",
      ].join("::");
    })
    .sort()
    .join("|");
}

import { toEditorGraph } from "../adapters/microflow-adapters";
import type { MicroflowEditorGraphPatch, MicroflowEditorNode, MicroflowSchema } from "../schema/types";
import type { MicroflowAutoLayoutInput, MicroflowLayoutBounds } from "./auto-layout-types";

const defaultLayerGap = 280;
const defaultNodeGap = 150;
const startX = 120;
const startY = 120;

export function createBusinessAutoLayoutPatch(input: MicroflowAutoLayoutInput): MicroflowEditorGraphPatch {
  const direction = input.options?.direction ?? "LR";
  const layerGap = input.options?.layerGap ?? defaultLayerGap;
  const nodeGap = input.options?.nodeGap ?? defaultNodeGap;
  const graph = toEditorGraph(input.schema);
  const nodes = graph.nodes.filter(node => input.collectionId
    ? node.collectionId === input.collectionId
    : !node.parentObjectId);
  const nodeIds = new Set(nodes.map(node => node.objectId));
  const outgoing = new Map<string, string[]>();
  const indegree = new Map<string, number>();

  for (const node of nodes) {
    outgoing.set(node.objectId, []);
    indegree.set(node.objectId, 0);
  }
  for (const edge of graph.edges) {
    const source = editorNodeIdToObjectId(edge.sourceNodeId);
    const target = editorNodeIdToObjectId(edge.targetNodeId);
    if (!nodeIds.has(source) || !nodeIds.has(target)) {
      continue;
    }
    outgoing.set(source, [...(outgoing.get(source) ?? []), target]);
    indegree.set(target, (indegree.get(target) ?? 0) + 1);
  }

  const layers = assignLayers(nodes, outgoing, indegree);
  const byLayer = new Map<number, MicroflowEditorNode[]>();
  for (const node of nodes) {
    const layer = layers.get(node.objectId) ?? 0;
    byLayer.set(layer, [...(byLayer.get(layer) ?? []), node]);
  }

  const movedNodes: NonNullable<MicroflowEditorGraphPatch["movedNodes"]> = [];
  for (const [layer, layerNodes] of byLayer.entries()) {
    const ordered = [...layerNodes].sort(branchAwareNodeSort(input.schema, layerNodes));
    ordered.forEach((node, index) => {
      const x = direction === "LR" ? startX + layer * layerGap : startX + index * nodeGap;
      const y = direction === "LR" ? startY + index * nodeGap : startY + layer * layerGap;
      movedNodes.push({ objectId: node.objectId, position: { x, y } });
    });
  }

  const resizedNodes: NonNullable<MicroflowEditorGraphPatch["resizedNodes"]> = [];
  for (const node of nodes) {
    if (node.nodeKind !== "loopedActivity") {
      continue;
    }
    const childCount = graph.nodes.filter(child => child.parentObjectId === node.objectId).length;
    if (childCount > 0) {
      resizedNodes.push({
        objectId: node.objectId,
        size: {
          width: Math.max(node.size.width, 300),
          height: Math.max(node.size.height, 132 + Math.min(childCount, 8) * 40),
        },
      });
    }
  }

  return { movedNodes, resizedNodes };
}

export function boundsFromLayoutPatch(schema: MicroflowSchema, patch: MicroflowEditorGraphPatch): MicroflowLayoutBounds {
  const graph = toEditorGraph(schema);
  const positionByObjectId = new Map((patch.movedNodes ?? []).map(item => [item.objectId, item.position]));
  const nodes = graph.nodes.filter(node => positionByObjectId.has(node.objectId));
  if (nodes.length === 0) {
    return { minX: 0, minY: 0, maxX: 0, maxY: 0, width: 0, height: 0 };
  }
  const minX = Math.min(...nodes.map(node => (positionByObjectId.get(node.objectId)?.x ?? node.position.x) - node.size.width / 2));
  const minY = Math.min(...nodes.map(node => (positionByObjectId.get(node.objectId)?.y ?? node.position.y) - node.size.height / 2));
  const maxX = Math.max(...nodes.map(node => (positionByObjectId.get(node.objectId)?.x ?? node.position.x) + node.size.width / 2));
  const maxY = Math.max(...nodes.map(node => (positionByObjectId.get(node.objectId)?.y ?? node.position.y) + node.size.height / 2));
  return { minX, minY, maxX, maxY, width: maxX - minX, height: maxY - minY };
}

function assignLayers(
  nodes: MicroflowEditorNode[],
  outgoing: Map<string, string[]>,
  indegree: Map<string, number>,
): Map<string, number> {
  const layers = new Map<string, number>();
  const seeds = nodes.filter(node => node.nodeKind === "startEvent").map(node => node.objectId);
  const fallbackSeeds = nodes.filter(node => (indegree.get(node.objectId) ?? 0) === 0).map(node => node.objectId);
  const queue = (seeds.length > 0 ? seeds : fallbackSeeds).map(objectId => ({ objectId, layer: 0 }));
  const visits = new Set<string>();

  while (queue.length > 0) {
    const current = queue.shift();
    if (!current) {
      continue;
    }
    const visitKey = `${current.objectId}:${current.layer}`;
    if (visits.has(visitKey)) {
      continue;
    }
    visits.add(visitKey);
    const existing = layers.get(current.objectId);
    if (existing === undefined || current.layer > existing) {
      layers.set(current.objectId, current.layer);
    }
    for (const target of outgoing.get(current.objectId) ?? []) {
      queue.push({ objectId: target, layer: current.layer + 1 });
    }
  }

  const maxLayer = Math.max(0, ...Array.from(layers.values()));
  let disconnectedLayer = maxLayer + 1;
  for (const node of nodes) {
    if (!layers.has(node.objectId)) {
      layers.set(node.objectId, disconnectedLayer);
      disconnectedLayer += 1;
    }
  }
  return layers;
}

function branchAwareNodeSort(schema: MicroflowSchema, nodes: MicroflowEditorNode[]) {
  const orderByObjectId = new Map<string, number>();
  for (const flow of schema.flows) {
    if (flow.kind === "sequence") {
      orderByObjectId.set(flow.destinationObjectId, orderByObjectId.size);
    }
  }
  return (left: MicroflowEditorNode, right: MicroflowEditorNode) => {
    const leftOrder = orderByObjectId.get(left.objectId) ?? Number.MAX_SAFE_INTEGER;
    const rightOrder = orderByObjectId.get(right.objectId) ?? Number.MAX_SAFE_INTEGER;
    return leftOrder - rightOrder || left.position.y - right.position.y || left.position.x - right.position.x;
  };
}

function editorNodeIdToObjectId(editorNodeId: string): string {
  return editorNodeId.startsWith("node-") ? editorNodeId.slice(5) : editorNodeId;
}

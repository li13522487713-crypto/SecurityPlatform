import { toEditorGraph } from "./microflow-adapters";
import type { MicroflowEditorGraphPatch, MicroflowSchema } from "../schema/types";

const layerGap = 280;
const rowGap = 150;
const startX = 120;
const startY = 120;

export function createAutoLayoutPatch(schema: MicroflowSchema): MicroflowEditorGraphPatch {
  const graph = toEditorGraph(schema);
  const rootNodes = graph.nodes.filter(node => !node.parentObjectId);
  const rootIds = new Set(rootNodes.map(node => node.objectId));
  const outgoing = new Map<string, string[]>();
  const indegree = new Map<string, number>();
  for (const node of rootNodes) {
    outgoing.set(node.objectId, []);
    indegree.set(node.objectId, 0);
  }
  for (const edge of graph.edges) {
    const source = editorNodeIdToObjectId(edge.sourceNodeId);
    const target = editorNodeIdToObjectId(edge.targetNodeId);
    if (!rootIds.has(source) || !rootIds.has(target)) {
      continue;
    }
    outgoing.set(source, [...(outgoing.get(source) ?? []), target]);
    indegree.set(target, (indegree.get(target) ?? 0) + 1);
  }

  const layers = new Map<string, number>();
  const seeds = rootNodes
    .filter(node => node.nodeKind === "startEvent")
    .map(node => node.objectId);
  const queue = (seeds.length > 0 ? seeds : rootNodes.filter(node => (indegree.get(node.objectId) ?? 0) === 0).map(node => node.objectId))
    .map(objectId => ({ objectId, layer: 0 }));

  while (queue.length > 0) {
    const current = queue.shift();
    if (!current) {
      continue;
    }
    const existing = layers.get(current.objectId);
    if (existing !== undefined && existing >= current.layer) {
      continue;
    }
    layers.set(current.objectId, current.layer);
    for (const target of outgoing.get(current.objectId) ?? []) {
      queue.push({ objectId: target, layer: current.layer + 1 });
    }
  }

  const maxLayer = Math.max(0, ...Array.from(layers.values()));
  let disconnectedLayer = maxLayer + 1;
  for (const node of rootNodes) {
    if (!layers.has(node.objectId)) {
      layers.set(node.objectId, disconnectedLayer);
      disconnectedLayer += 1;
    }
  }

  const byLayer = new Map<number, typeof rootNodes>();
  for (const node of rootNodes) {
    const layer = layers.get(node.objectId) ?? 0;
    byLayer.set(layer, [...(byLayer.get(layer) ?? []), node]);
  }

  const movedNodes: MicroflowEditorGraphPatch["movedNodes"] = [];
  for (const [layer, nodes] of byLayer.entries()) {
    const ordered = [...nodes].sort((a, b) => a.position.y - b.position.y || a.position.x - b.position.x);
    ordered.forEach((node, index) => {
      movedNodes.push({
        objectId: node.objectId,
        position: {
          x: startX + layer * layerGap,
          y: startY + index * rowGap,
        },
      });
    });
  }

  return { movedNodes };
}

function editorNodeIdToObjectId(editorNodeId: string): string {
  return editorNodeId.startsWith("node-") ? editorNodeId.slice(5) : editorNodeId;
}

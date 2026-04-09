import type { DesignerEdge, DesignerNode } from "../graph/index";

export function validateGraph(nodes: DesignerNode[], edges: DesignerEdge[]) {
  const nodeIds = new Set(nodes.map((node) => node.id));
  const danglingEdges = edges.filter((edge) => !nodeIds.has(edge.source) || !nodeIds.has(edge.target));
  return {
    valid: danglingEdges.length === 0,
    danglingEdges
  };
}

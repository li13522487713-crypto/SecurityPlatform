import type { DesignerEdge, DesignerNode } from "../graph/index";

export function serializeGraph(nodes: DesignerNode[], edges: DesignerEdge[]) {
  return JSON.stringify({ nodes, edges });
}

export function deserializeGraph(payload: string) {
  return JSON.parse(payload) as {
    nodes: DesignerNode[];
    edges: DesignerEdge[];
  };
}

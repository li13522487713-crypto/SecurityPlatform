import type { DesignerEdge, DesignerNode } from "../graph/index";

export function compileDesignerOutput(nodes: DesignerNode[], edges: DesignerEdge[]) {
  return {
    nodeCount: nodes.length,
    edgeCount: edges.length,
    generatedAt: new Date().toISOString()
  };
}

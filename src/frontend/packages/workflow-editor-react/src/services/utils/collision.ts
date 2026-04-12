import type { CanvasNode } from "../../editor/workflow-editor-state";

export interface CollisionTransform {
  node: CanvasNode;
  bounds: { left: number; top: number; right: number; bottom: number };
}

export function getCollisionTransform(
  point: { x: number; y: number },
  nodes: CanvasNode[],
  nodeSize: { width: number; height: number }
): CollisionTransform | undefined {
  const x = point.x;
  const y = point.y;
  for (let i = nodes.length - 1; i >= 0; i -= 1) {
    const node = nodes[i];
    const bounds = {
      left: node.x,
      top: node.y,
      right: node.x + nodeSize.width,
      bottom: node.y + nodeSize.height
    };
    if (x >= bounds.left && x <= bounds.right && y >= bounds.top && y <= bounds.bottom) {
      return { node, bounds };
    }
  }
  return undefined;
}

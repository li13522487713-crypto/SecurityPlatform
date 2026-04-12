import type { CanvasNode } from "../../editor/workflow-editor-state";

interface Point {
  x: number;
  y: number;
}

interface Size {
  width: number;
  height: number;
}

function intersects(a: { x: number; y: number; width: number; height: number }, b: { x: number; y: number; width: number; height: number }): boolean {
  return !(a.x + a.width <= b.x || b.x + b.width <= a.x || a.y + a.height <= b.y || b.y + b.height <= a.y);
}

export function getAntiOverlapPosition(
  nodes: CanvasNode[],
  params: {
    position: Point;
    size: Size;
    step?: number;
    maxTry?: number;
  }
): Point {
  const step = params.step ?? 36;
  const maxTry = params.maxTry ?? 120;
  const baseRect = {
    x: Math.round(params.position.x),
    y: Math.round(params.position.y),
    width: params.size.width,
    height: params.size.height
  };

  const occupied = nodes.map((item) => ({
    x: item.x,
    y: item.y,
    width: params.size.width,
    height: params.size.height
  }));

  if (!occupied.some((item) => intersects(baseRect, item))) {
    return { x: baseRect.x, y: baseRect.y };
  }

  for (let i = 1; i <= maxTry; i += 1) {
    const ring = Math.ceil(Math.sqrt(i));
    const angle = (i * Math.PI) / 4;
    const candidate = {
      x: Math.max(24, Math.round(baseRect.x + Math.cos(angle) * ring * step)),
      y: Math.max(24, Math.round(baseRect.y + Math.sin(angle) * ring * step)),
      width: baseRect.width,
      height: baseRect.height
    };
    if (!occupied.some((item) => intersects(candidate, item))) {
      return { x: candidate.x, y: candidate.y };
    }
  }

  return { x: baseRect.x + step, y: baseRect.y + step };
}

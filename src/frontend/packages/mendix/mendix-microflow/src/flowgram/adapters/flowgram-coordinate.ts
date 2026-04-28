import type { MicroflowPoint } from "../../schema";

export interface FlowGramViewportLike {
  x: number;
  y: number;
  zoom: number;
}

export function getFlowGramCanvasContainerRect(element: HTMLElement | null): DOMRect | undefined {
  return element?.getBoundingClientRect();
}

export function clientPointToFlowGramPoint(
  clientPoint: MicroflowPoint,
  rect: Pick<DOMRect, "left" | "top"> | undefined,
  viewport: FlowGramViewportLike,
): MicroflowPoint {
  const localX = clientPoint.x - (rect?.left ?? 0);
  const localY = clientPoint.y - (rect?.top ?? 0);
  const zoom = viewport.zoom || 1;
  return {
    x: (localX - viewport.x) / zoom,
    y: (localY - viewport.y) / zoom,
  };
}

export function flowGramPointToAuthoringPoint(point: MicroflowPoint): MicroflowPoint {
  return point;
}

export function snapMicroflowPoint(point: MicroflowPoint, gridSize = 16): MicroflowPoint {
  return {
    x: Math.round(point.x / gridSize) * gridSize,
    y: Math.round(point.y / gridSize) * gridSize,
  };
}


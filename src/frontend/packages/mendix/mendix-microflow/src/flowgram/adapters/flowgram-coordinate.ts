import type { MicroflowPoint } from "../../schema";

export const MICROFLOW_GRID_SIZE = 24;
export const LOOP_HEADER_OFFSET_PX = 76;

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
    x: (localX + viewport.x) / zoom,
    y: (localY + viewport.y) / zoom,
  };
}

export function flowGramPointToAuthoringPoint(point: MicroflowPoint): MicroflowPoint {
  return point;
}

export function normalizeFlowGramPoint(point: Partial<MicroflowPoint> | undefined): MicroflowPoint | undefined {
  if (!point) {
    return undefined;
  }
  const x = Number(point.x);
  const y = Number(point.y);
  if (!Number.isFinite(x) || !Number.isFinite(y)) {
    return undefined;
  }
  return { x, y };
}

export function snapMicroflowPoint(point: MicroflowPoint, gridSize = MICROFLOW_GRID_SIZE): MicroflowPoint {
  if (!Number.isFinite(point.x) || !Number.isFinite(point.y) || gridSize <= 1) {
    return point;
  }

  return {
    x: Math.round(point.x / gridSize) * gridSize,
    y: Math.round(point.y / gridSize) * gridSize,
  };
}

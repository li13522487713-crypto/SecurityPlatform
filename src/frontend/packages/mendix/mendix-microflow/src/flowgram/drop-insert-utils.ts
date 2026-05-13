export interface DropInsertEdgeCandidate {
  flowId: string;
  edgeKind?: string;
  sourcePoint: { x: number; y: number };
  targetPoint: { x: number; y: number };
}

const defaultBlockedEdgeKinds = new Set(["annotation", "errorHandler"]);

export function pointToSegmentDistance(
  point: { x: number; y: number },
  source: { x: number; y: number },
  target: { x: number; y: number },
): number {
  const dx = target.x - source.x;
  const dy = target.y - source.y;
  const lengthSquared = dx * dx + dy * dy;
  if (lengthSquared === 0) {
    return Math.hypot(point.x - source.x, point.y - source.y);
  }
  const t = Math.max(0, Math.min(1, ((point.x - source.x) * dx + (point.y - source.y) * dy) / lengthSquared));
  return Math.hypot(point.x - (source.x + t * dx), point.y - (source.y + t * dy));
}

export function findNearestInsertableEdgeFlowId(
  edges: DropInsertEdgeCandidate[],
  point: { x: number; y: number },
  threshold = 24,
  blockedEdgeKinds: ReadonlySet<string> = defaultBlockedEdgeKinds,
): string | undefined {
  let nearest: { flowId: string; distance: number } | undefined;
  for (const edge of edges) {
    if (blockedEdgeKinds.has(String(edge.edgeKind ?? ""))) {
      continue;
    }
    const distance = pointToSegmentDistance(point, edge.sourcePoint, edge.targetPoint);
    if (distance <= threshold && (!nearest || distance < nearest.distance)) {
      nearest = { flowId: edge.flowId, distance };
    }
  }
  return nearest?.flowId;
}

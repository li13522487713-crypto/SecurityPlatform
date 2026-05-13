import { describe, expect, it } from "vitest";

import { findNearestInsertableEdgeFlowId, pointToSegmentDistance } from "./drop-insert-utils";

describe("drop-insert-utils", () => {
  it("computes point-to-segment distance", () => {
    const distanceOnSegment = pointToSegmentDistance(
      { x: 5, y: 0 },
      { x: 0, y: 0 },
      { x: 10, y: 0 },
    );
    const distanceOffSegment = pointToSegmentDistance(
      { x: 5, y: 12 },
      { x: 0, y: 0 },
      { x: 10, y: 0 },
    );

    expect(distanceOnSegment).toBeCloseTo(0, 6);
    expect(distanceOffSegment).toBeCloseTo(12, 6);
  });

  it("prefers nearest insertable edge within default 24px threshold", () => {
    const flowId = findNearestInsertableEdgeFlowId(
      [
        {
          flowId: "edge-far",
          edgeKind: "sequence",
          sourcePoint: { x: 0, y: 0 },
          targetPoint: { x: 100, y: 0 },
        },
        {
          flowId: "edge-near",
          edgeKind: "sequence",
          sourcePoint: { x: 0, y: 20 },
          targetPoint: { x: 100, y: 20 },
        },
      ],
      { x: 40, y: 24 },
    );
    expect(flowId).toBe("edge-near");
  });

  it("skips blocked edge kinds and respects threshold", () => {
    const blockedOnly = findNearestInsertableEdgeFlowId(
      [
        {
          flowId: "edge-annotation",
          edgeKind: "annotation",
          sourcePoint: { x: 0, y: 0 },
          targetPoint: { x: 100, y: 0 },
        },
        {
          flowId: "edge-error",
          edgeKind: "errorHandler",
          sourcePoint: { x: 0, y: 10 },
          targetPoint: { x: 100, y: 10 },
        },
      ],
      { x: 50, y: 8 },
    );
    expect(blockedOnly).toBeUndefined();

    const aboveThreshold = findNearestInsertableEdgeFlowId(
      [
        {
          flowId: "edge-sequence",
          edgeKind: "sequence",
          sourcePoint: { x: 0, y: 0 },
          targetPoint: { x: 100, y: 0 },
        },
      ],
      { x: 50, y: 25 },
      24,
    );
    expect(aboveThreshold).toBeUndefined();
  });
});

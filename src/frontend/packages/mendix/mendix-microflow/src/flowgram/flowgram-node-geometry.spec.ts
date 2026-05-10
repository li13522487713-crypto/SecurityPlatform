import { describe, expect, it } from "vitest";

import {
  getMendixMicroflowDropOffset,
  getMendixMicroflowNodeSize,
} from "./flowgram-node-geometry";

describe("flowgram node geometry", () => {
  it("keeps declared FlowGram node sizes aligned with compact Mendix DOM geometry", () => {
    expect(getMendixMicroflowNodeSize("startEvent")).toEqual({ width: 80, height: 28 });
    expect(getMendixMicroflowNodeSize("endEvent")).toEqual({ width: 80, height: 28 });
    expect(getMendixMicroflowNodeSize("actionActivity")).toEqual({ width: 56, height: 56 });
    expect(getMendixMicroflowNodeSize("exclusiveSplit")).toEqual({ width: 40, height: 40 });
    expect(getMendixMicroflowNodeSize("inclusiveGateway")).toEqual({ width: 40, height: 40 });
    expect(getMendixMicroflowNodeSize("loopedActivity")).toEqual({ width: 320, height: 190 });
  });

  it("centers palette drops using the same geometry used by FlowGram ports", () => {
    expect(getMendixMicroflowDropOffset("actionActivity")).toEqual({ x: 28, y: 28 });
    expect(getMendixMicroflowDropOffset("exclusiveSplit")).toEqual({ x: 20, y: 20 });
  });
});

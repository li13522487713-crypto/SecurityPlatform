import { describe, expect, it } from "vitest";

import {
  getMendixMicroflowDropOffset,
  getMendixMicroflowNodeSize,
} from "./flowgram-node-geometry";

describe("flowgram node geometry", () => {
  it("keeps declared FlowGram node sizes aligned with compact Mendix DOM geometry", () => {
    expect(getMendixMicroflowNodeSize("startEvent")).toEqual({ width: 18, height: 18 });
    expect(getMendixMicroflowNodeSize("endEvent")).toEqual({ width: 18, height: 18 });
    expect(getMendixMicroflowNodeSize("actionActivity")).toEqual({ width: 110, height: 36 });
    expect(getMendixMicroflowNodeSize("exclusiveSplit")).toEqual({ width: 44, height: 44 });
    expect(getMendixMicroflowNodeSize("inclusiveGateway")).toEqual({ width: 44, height: 44 });
    expect(getMendixMicroflowNodeSize("exclusiveMerge")).toEqual({ width: 24, height: 24 });
    expect(getMendixMicroflowNodeSize("loopedActivity")).toEqual({ width: 320, height: 190 });
  });

  it("centers palette drops using the same geometry used by FlowGram ports", () => {
    expect(getMendixMicroflowDropOffset("actionActivity")).toEqual({ x: 55, y: 18 });
    expect(getMendixMicroflowDropOffset("exclusiveSplit")).toEqual({ x: 22, y: 22 });
  });
});

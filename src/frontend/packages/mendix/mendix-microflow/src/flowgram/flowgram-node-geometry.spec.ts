import { describe, expect, it } from "vitest";

import {
  getMendixMicroflowDropOffset,
  getMendixMicroflowNodeSize,
} from "./flowgram-node-geometry";

describe("flowgram node geometry", () => {
  it("keeps declared FlowGram node sizes aligned with compact Mendix DOM geometry", () => {
    expect(getMendixMicroflowNodeSize("startEvent")).toEqual({ width: 28, height: 28 });
    expect(getMendixMicroflowNodeSize("endEvent")).toEqual({ width: 28, height: 28 });
    expect(getMendixMicroflowNodeSize("continueEvent")).toEqual({ width: 28, height: 28 });
    expect(getMendixMicroflowNodeSize("breakEvent")).toEqual({ width: 28, height: 28 });
    expect(getMendixMicroflowNodeSize("errorEvent")).toEqual({ width: 22, height: 22 });
    expect(getMendixMicroflowNodeSize("parameterObject")).toEqual({ width: 72, height: 72 });
    expect(getMendixMicroflowNodeSize("actionActivity")).toEqual({ width: 72, height: 72 });
    expect(getMendixMicroflowNodeSize("exclusiveSplit")).toEqual({ width: 70, height: 70 });
    expect(getMendixMicroflowNodeSize("inclusiveGateway")).toEqual({ width: 70, height: 70 });
    expect(getMendixMicroflowNodeSize("exclusiveMerge")).toEqual({ width: 24, height: 24 });
    expect(getMendixMicroflowNodeSize("loopedActivity")).toEqual({ width: 320, height: 190 });
  });

  it("centers palette drops using the same geometry used by FlowGram ports", () => {
    expect(getMendixMicroflowDropOffset("actionActivity")).toEqual({ x: 36, y: 36 });
    expect(getMendixMicroflowDropOffset("exclusiveSplit")).toEqual({ x: 35, y: 35 });
  });
});

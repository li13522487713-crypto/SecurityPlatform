import { describe, expect, it, vi } from "vitest";

vi.mock("@douyinfe/semi-ui", () => ({
  Input: () => null,
}));

vi.mock("@flowgram-adapter/free-layout-editor", () => ({
  usePlaygroundReadonlyState: () => false,
}));

import type { FlowGramMicroflowEdgeData } from "./FlowGramMicroflowTypes";
import { lineClassNameFromEdgeData, lineLabelFromEdgeData } from "./FlowGramMicroflowLineRenderer";

function edgeData(patch: Partial<FlowGramMicroflowEdgeData>): FlowGramMicroflowEdgeData {
  return {
    flowId: "flow-1",
    flowKind: "sequence",
    edgeKind: "sequence",
    isErrorHandler: false,
    caseValues: [],
    validationState: "valid",
    ...patch,
  };
}

describe("FlowGramMicroflowLineRenderer helpers", () => {
  it("derives decision labels from case values", () => {
    expect(lineLabelFromEdgeData(edgeData({
      edgeKind: "decisionCondition",
      caseValues: [{ kind: "boolean", officialType: "Microflows$EnumerationCase", value: true, persistedValue: "true" }],
    }))).toBe("true");
    expect(lineLabelFromEdgeData(edgeData({
      edgeKind: "decisionCondition",
      caseValues: [{ kind: "fallback", officialType: "Microflows$NoCase" }],
    }))).toBe("else");
    expect(lineLabelFromEdgeData(edgeData({
      edgeKind: "decisionCondition",
      caseValues: [{ kind: "empty", officialType: "Microflows$NoCase" }],
    }))).toBe("(empty)");
    expect(lineLabelFromEdgeData(edgeData({
      edgeKind: "decisionCondition",
      caseValues: [{ kind: "noCase", officialType: "Microflows$NoCase" }],
    }))).toBe("(empty)");
  });

  it("adds semantic class names for validation and runtime state", () => {
    expect(lineClassNameFromEdgeData(edgeData({
      edgeKind: "errorHandler",
      validationState: "error",
      runtimeState: "errorHandlerVisited",
    }))).toContain("microflow-flowgram-line--errorHandler");
    expect(lineClassNameFromEdgeData(edgeData({
      edgeKind: "errorHandler",
      validationState: "error",
      runtimeState: "errorHandlerVisited",
    }))).toContain("is-runtime-errorHandlerVisited");
  });

  it("marks annotation edges with the annotation semantic class", () => {
    expect(lineClassNameFromEdgeData(edgeData({
      flowKind: "annotation",
      edgeKind: "annotation",
    }))).toContain("microflow-flowgram-line--annotation");
  });

  it("does not label ordinary sequence edges without explicit branch data", () => {
    expect(lineLabelFromEdgeData(edgeData({ edgeKind: "sequence", caseValues: [] }))).toBe("");
  });
});

import { describe, expect, it } from "vitest";

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
    }))).toBe("default");
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
});

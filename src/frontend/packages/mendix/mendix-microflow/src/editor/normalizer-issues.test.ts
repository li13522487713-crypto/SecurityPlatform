import { describe, expect, it } from "vitest";
import { createNormalizerIssues } from "./normalizer-issues";

describe("createNormalizerIssues", () => {
  it("keeps duplicate id issues distinct by field path", () => {
    const issues = createNormalizerIssues("mf-1", [
      {
        code: "MF_FLOW_ID_DUPLICATED",
        severity: "error",
        flowId: "duplicate-flow",
        fieldPath: "flows[0].id",
        message: "Flow id duplicate-flow is duplicated in the microflow schema.",
      },
      {
        code: "MF_FLOW_ID_DUPLICATED",
        severity: "error",
        flowId: "duplicate-flow",
        fieldPath: "flows[1].id",
        message: "Flow id duplicate-flow is duplicated in the microflow schema.",
      },
    ]);

    expect(issues.map(issue => issue.id)).toEqual([
      "mf-1:normalizer:MF_FLOW_ID_DUPLICATED:duplicate-flow:flows[0].id",
      "mf-1:normalizer:MF_FLOW_ID_DUPLICATED:duplicate-flow:flows[1].id",
    ]);
    expect(new Set(issues.map(issue => issue.id)).size).toBe(2);
    expect(issues.every(issue => issue.blockSave && issue.blockPublish)).toBe(true);
  });
});

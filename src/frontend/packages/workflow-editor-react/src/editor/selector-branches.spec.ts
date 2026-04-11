import { describe, expect, it } from "vitest";
import {
  addSelectorCondition,
  deriveSelectorOutputPortKeys,
  normalizeSelectorConditions,
  reorderSelectorCondition
} from "./selector-branches";

describe("selector-branches", () => {
  it("normalizes branch ids", () => {
    const result = normalizeSelectorConditions([{ left: "a" }, { left: "b", branchId: "true_8" }]);
    expect(result.map((item) => item.branchId)).toEqual(["true", "true_8"]);
  });

  it("allocates branch id when adding condition", () => {
    const result = addSelectorCondition(normalizeSelectorConditions([{ left: "a", branchId: "true" }]));
    expect(result[1]?.branchId).toBe("true_1");
  });

  it("keeps branch id stable after reorder", () => {
    const conditions = normalizeSelectorConditions([{ left: "a", branchId: "true" }, { left: "b", branchId: "true_2" }]);
    const moved = reorderSelectorCondition(conditions, 1, 0);
    expect(moved.map((item) => item.branchId)).toEqual(["true_2", "true"]);
  });

  it("derives output ports from branch ids", () => {
    const ports = deriveSelectorOutputPortKeys([{ branchId: "true" }, { branchId: "true_risk" }]);
    expect(ports).toEqual(["true", "true_risk", "false"]);
  });
});

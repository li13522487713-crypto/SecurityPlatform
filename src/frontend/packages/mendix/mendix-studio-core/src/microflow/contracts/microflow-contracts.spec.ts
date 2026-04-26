import { describe, expect, it } from "vitest";

import { verifyMicroflowContracts } from "./verify-microflow-contracts";

describe("verifyMicroflowContracts", () => {
  it("all manifest samples + adapters pass", () => {
    const result = verifyMicroflowContracts();
    expect(result.errors, result.errors.join("\n")).toEqual([]);
    expect(result.ok).toBe(true);
    expect(result.sampleKeys.length).toBeGreaterThanOrEqual(7);
  });
});

import { describe, expect, it } from "vitest";
import { sampleMicroflowSchema } from "../schema/sample";
import { computeAvailableVariables, getMicroflowParameters, getVariablesBeforeObject } from "./variable-scope";

describe("variable-scope", () => {
  it("exposes declared microflow parameters as variables", () => {
    const parameters = getMicroflowParameters(sampleMicroflowSchema);
    const names = parameters.map(item => item.name);
    expect(names).toContain("$orderId");
    expect(names).toContain("$member");
  });

  it("provides loop iterator variables inside loop body scope", () => {
    const variables = getVariablesBeforeObject(sampleMicroflowSchema, "loop-log-line");
    const names = variables.map(item => item.name);
    expect(names).toContain("$orderLine");
    expect(names).toContain("$currentIndex");
  });

  it("includes iterator and parameter variables in available expression scope", () => {
    const variables = computeAvailableVariables(sampleMicroflowSchema, {
      objectId: "loop-log-line",
    });
    const names = variables.map(item => item.name);
    expect(names).toContain("$orderLine");
    expect(names).toContain("$orderId");
  });
});

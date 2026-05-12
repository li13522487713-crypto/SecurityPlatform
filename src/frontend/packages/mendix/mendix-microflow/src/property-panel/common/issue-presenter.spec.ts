import { describe, expect, it } from "vitest";

import type { MicroflowValidationIssue } from "../../schema";
import { dedupeIssues, presentIssueMessage } from "./issue-presenter";

function issue(overrides: Partial<MicroflowValidationIssue>): MicroflowValidationIssue {
  return {
    id: "issue-1",
    severity: "error",
    code: "MF_UNKNOWN",
    source: "variable",
    message: "invalid",
    ...overrides,
  };
}

describe("issue presenter", () => {
  it("maps known issue codes to human-readable messages", () => {
    const message = presentIssueMessage(issue({
      code: "MF_VARIABLE_NAME_REQUIRED",
      message: "MF_VARIABLE_NAME_REQUIRED",
    }));
    expect(message).toBe("Variable name is required.");
  });

  it("falls back to generic message for unknown MF_* codes", () => {
    const message = presentIssueMessage(issue({
      code: "MF_SOMETHING_NEW",
      message: "MF_SOMETHING_NEW",
    }));
    expect(message).toBe("Validation issue on this field.");
  });

  it("keeps custom non-MF messages", () => {
    const message = presentIssueMessage(issue({
      code: "ANY_CODE",
      message: "Entity metadata is not loaded.",
    }));
    expect(message).toBe("Entity metadata is not loaded.");
  });

  it("dedupes by code + fieldPath", () => {
    const issues = [
      issue({ id: "1", code: "MF_VARIABLE_NAME_REQUIRED", fieldPath: "action.outputVariableName" }),
      issue({ id: "2", code: "MF_VARIABLE_NAME_REQUIRED", fieldPath: "action.outputVariableName" }),
      issue({ id: "3", code: "MF_VARIABLE_NAME_REQUIRED", fieldPath: "action.returnValue.outputVariableName" }),
    ];
    const deduped = dedupeIssues(issues);

    expect(deduped).toHaveLength(2);
    expect(deduped.map(item => item.id)).toEqual(["1", "3"]);
  });
});

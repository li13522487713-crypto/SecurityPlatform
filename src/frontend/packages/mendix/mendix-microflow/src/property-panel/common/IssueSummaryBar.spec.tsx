// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { MicroflowValidationIssue } from "../../schema";
import { IssueSummaryBar } from "./IssueSummaryBar";

vi.mock("@douyinfe/semi-ui", () => ({
  Button: ({ children, onClick }: any) => <button onClick={onClick}>{children}</button>,
  Space: ({ children }: any) => <div>{children}</div>,
  Typography: {
    Text: ({ children }: any) => <span>{children}</span>,
  },
}));

function issue(overrides: Partial<MicroflowValidationIssue>): MicroflowValidationIssue {
  return {
    id: "issue-1",
    severity: "error",
    code: "MF_VARIABLE_NAME_REQUIRED",
    source: "variable",
    message: "MF_VARIABLE_NAME_REQUIRED",
    fieldPath: "action.outputVariableName",
    ...overrides,
  };
}

afterEach(() => cleanup());

describe("IssueSummaryBar", () => {
  it("shows deduped error and warning counts", () => {
    render(
      <IssueSummaryBar
        issues={[
          issue({ id: "1" }),
          issue({ id: "2" }),
          issue({ id: "3", severity: "warning", code: "MF_VARIABLE_OUTPUT_TYPE_UNKNOWN", message: "MF_VARIABLE_OUTPUT_TYPE_UNKNOWN", fieldPath: "action.entityQualifiedName" }),
          issue({ id: "4", severity: "warning", code: "MF_VARIABLE_OUTPUT_TYPE_UNKNOWN", message: "MF_VARIABLE_OUTPUT_TYPE_UNKNOWN", fieldPath: "action.entityQualifiedName" }),
        ]}
      />,
    );

    expect(screen.getByText("✕ 1 errors")).toBeTruthy();
    expect(screen.getByText("⚠ 1 warnings")).toBeTruthy();
    expect(screen.queryByText("Variable name is required.")).toBeNull();
  });

  it("expands details and locates field with human-readable message", () => {
    const onLocateField = vi.fn();
    render(
      <IssueSummaryBar
        issues={[
          issue({ id: "1", fieldPath: "action.outputVariableName" }),
        ]}
        onLocateField={onLocateField}
      />,
    );

    fireEvent.click(screen.getByText("展开"));
    const detail = screen.getByText("Variable name is required.");
    expect(detail).toBeTruthy();
    expect(screen.queryByText("MF_VARIABLE_NAME_REQUIRED")).toBeNull();

    fireEvent.click(detail.closest("button")!);
    expect(onLocateField).toHaveBeenCalledWith("action.outputVariableName");
  });
});

// @vitest-environment jsdom

import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { TryCatchForm } from "./try-catch-form";

vi.mock("@douyinfe/semi-ui", () => ({
  Input: ({ value, disabled, onChange, placeholder }: any) => <input value={value ?? ""} disabled={disabled} placeholder={placeholder} onChange={event => onChange?.(event.currentTarget.value)} />,
  Tooltip: ({ children }: any) => <>{children}</>,
  Typography: {
    Text: ({ children }: any) => <span>{children}</span>,
  },
}));

vi.mock("../panel-shared", () => ({
  Field: ({ label, children }: any) => <section data-testid={`field-${String(label)}`}>{children}</section>,
}));

afterEach(() => cleanup());

describe("TryCatchForm", () => {
  it("renders Try/Catch/Finally collapsible sections with key fields", () => {
    render(
      <TryCatchForm
        object={{
          id: "try-catch-1",
          stableId: "try-catch-1",
          kind: "tryCatch",
          officialType: "Microflows$TryCatch",
          caption: "Try/Catch",
          documentation: "",
          relativeMiddlePoint: { x: 0, y: 0 },
          size: { width: 180, height: 80 },
          ports: [],
          editor: {},
          tryBranchKey: "try",
          catchBranchKey: "catch",
          finallyBranchKey: "finally",
          errorVariableName: "latestError",
        } as any}
        patch={vi.fn()}
      />,
    );

    expect(screen.getByText("Try")).toBeTruthy();
    expect(screen.getByText("Catch")).toBeTruthy();
    expect(screen.getByText("Finally")).toBeTruthy();
    expect(screen.getByTestId("field-Try Branch Key")).toBeTruthy();
    expect(screen.getByTestId("field-Catch Branch Key")).toBeTruthy();
    expect(screen.getByTestId("field-Error Variable Name")).toBeTruthy();
    expect(screen.getByTestId("field-Finally Branch Key")).toBeTruthy();
  });
});


// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { ObjectBaseForm } from "./object-base-form";

vi.mock("@douyinfe/semi-ui", () => ({
  Input: ({ value, onChange, disabled }: any) => <input value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)} />,
  Select: ({ value, onChange, disabled, optionList }: any) => (
    <select value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)}>
      {(optionList ?? []).map((option: any) => <option key={option.value} value={option.value}>{option.label ?? option.value}</option>)}
    </select>
  ),
  Switch: ({ checked, onChange, disabled }: any) => (
    <input type="checkbox" checked={Boolean(checked)} disabled={disabled} onChange={event => onChange?.(event.currentTarget.checked)} />
  ),
  TextArea: ({ value, onChange, disabled }: any) => <textarea value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)} />,
  Tooltip: ({ children }: any) => <>{children}</>,
  Typography: {
    Text: ({ children }: any) => <span>{children}</span>,
  },
}));

vi.mock("../panel-shared", () => ({
  Field: ({ label, children }: any) => <section data-testid={`field-${String(label)}`}>{children}</section>,
}));

afterEach(() => cleanup());

describe("ObjectBaseForm background color", () => {
  it("shows common background color selector for non-event non-action nodes", () => {
    const patch = vi.fn();
    render(
      <ObjectBaseForm
        object={{
          id: "decision-1",
          stableId: "decision-1",
          kind: "exclusiveSplit",
          officialType: "Microflows$ExclusiveSplit",
          caption: "Decision",
          documentation: "",
          backgroundColor: "default",
          relativeMiddlePoint: { x: 100, y: 120 },
          size: { width: 160, height: 76 },
          editor: {},
          splitCondition: { kind: "expression", expression: { raw: "$flag = true" }, resultType: "boolean" },
          errorHandlingType: "rollback",
        } as any}
        patch={patch}
      />,
    );

    expect(screen.getByTestId("field-Background Color")).toBeTruthy();
    fireEvent.change(screen.getByDisplayValue("default"), { target: { value: "purple" } });
    expect(patch).toHaveBeenCalledWith(expect.objectContaining({ backgroundColor: "purple" }));
  });

  it("does not show common background color selector for event nodes", () => {
    render(
      <ObjectBaseForm
        object={{
          id: "start-1",
          stableId: "start-1",
          kind: "startEvent",
          officialType: "Microflows$StartEvent",
          caption: "Start",
          documentation: "",
          relativeMiddlePoint: { x: 0, y: 0 },
          size: { width: 18, height: 18 },
          editor: {},
          trigger: { type: "manual" },
        } as any}
        patch={vi.fn()}
      />,
    );

    expect(screen.queryByTestId("field-Background Color")).toBeNull();
  });
});

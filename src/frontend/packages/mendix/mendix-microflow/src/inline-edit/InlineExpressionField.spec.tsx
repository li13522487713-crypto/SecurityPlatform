// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

vi.mock("@douyinfe/semi-ui", () => ({
  Input: (props: { value?: string; onChange?: (value: string) => void }) => (
    <input
      data-testid="inline-input"
      value={props.value ?? ""}
      onChange={event => props.onChange?.(event.currentTarget.value)}
    />
  ),
  Button: ({ children, onClick }: { children?: React.ReactNode; onClick?: () => void }) => (
    <button type="button" onClick={onClick}>{children}</button>
  ),
  Popover: ({ children }: { children?: React.ReactNode }) => <div>{children}</div>,
  Select: (props: { optionList?: Array<{ label: string; value: string }>; onChange?: (value?: string) => void }) => (
    <select data-testid="picker-select" onChange={event => props.onChange?.(event.currentTarget.value)}>
      <option value="" />
      {(props.optionList ?? []).map(item => <option key={item.value} value={item.value}>{item.label}</option>)}
    </select>
  ),
  Typography: { Text: ({ children }: { children?: React.ReactNode }) => <span>{children}</span> },
}));

import { InlineExpressionField } from "./InlineExpressionField";

afterEach(() => {
  cleanup();
});

describe("InlineExpressionField", () => {
  it("appends selected variable token to current expression", () => {
    const onCommit = vi.fn();
    render(
      <InlineExpressionField
        value="POST /api/incidents"
        onCommit={onCommit}
        options={[{ label: "inputs::incidentId|type=string", value: "$incidentId" }]}
      />,
    );
    fireEvent.change(screen.getByTestId("picker-select"), { target: { value: "$incidentId" } });
    expect(onCommit).toHaveBeenCalledWith("POST /api/incidents $incidentId");
  });

  it("commits selected variable directly when expression is empty", () => {
    const onCommit = vi.fn();
    render(
      <InlineExpressionField
        value=""
        onCommit={onCommit}
        options={[{ label: "context::riskScore|type=number", value: "$riskScore" }]}
      />,
    );
    fireEvent.change(screen.getByTestId("picker-select"), { target: { value: "$riskScore" } });
    expect(onCommit).toHaveBeenCalledWith("$riskScore");
  });
});

// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

vi.mock("@douyinfe/semi-ui", () => ({
  Button: ({ onClick, children }: { onClick?: () => void; children?: React.ReactNode }) => <button type="button" onClick={onClick}>{children}</button>,
  Space: ({ children }: { children?: React.ReactNode }) => <div>{children}</div>,
  Select: (props: { optionList?: Array<{ label: string; value: string }>; onChange?: (value?: string) => void; placeholder?: string }) => (
    <select data-testid={`select-${props.placeholder ?? "default"}`} onChange={event => props.onChange?.(event.currentTarget.value)}>
      <option value="" />
      {(props.optionList ?? []).map(item => <option key={item.value} value={item.value}>{item.label}</option>)}
    </select>
  ),
  Typography: { Text: ({ children }: { children?: React.ReactNode }) => <span>{children}</span> },
}));

import { ConditionBuilder } from "./ConditionBuilder";

afterEach(() => {
  cleanup();
});

describe("ConditionBuilder", () => {
  it("appends selected variable into left side value", () => {
    const onChange = vi.fn();
    render(
      <ConditionBuilder
        value={{ left: "$riskScore", operator: "greater or equal", right: "80", logic: "AND" }}
        variables={[{ name: "$incidentId", source: "input" }]}
        onChange={onChange}
      />,
    );
    fireEvent.change(screen.getByTestId("select-从上下文插入变量"), { target: { value: "$incidentId" } });
    const next = onChange.mock.calls.at(-1)?.[0];
    expect(next.left).toBe("$riskScore $incidentId");
  });
});

